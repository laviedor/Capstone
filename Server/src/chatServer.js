const net = require('net');
const chatProtocol = require('./chatProtocol');
const accounts = require('./accounts');
const friends = require('./friends');
const presence = require('./presence');
const worlds = require('./worlds');
const buildings = require('./buildings');
const resources = require('./resources');
const production = require('./production');
const gathering = require('./gathering');
const catalog = require('./catalog');
const { MAX_LINE_LENGTH, MULTIVERSE, PRODUCTION_TICK_MS, OFFLINE_NOTICE_SECONDS } = require('./config');

// TCP 서버: 채팅, 월드/구역 이동, 건물 변경(건설, 철거, 이동), 채집, 재화 알림
//   채팅은 모든 접속자에게, 건물 변경은 같은 월드에 있는 사람에게 전달한다 (worlds.js)
//   건물은 DB 에 저장하고(buildings.js) 건설 비용, 철거 환급, 건물 생산(production.js)을 서버가 계산해
//   재화가 바뀔 때마다 그 유저에게 알려 준다
//   필드의 자원 노드, 채집 타이머의 보상도 서버가 정해서 재화에 더한다 (gathering.js)
//   로그인 토큰으로 입장한 접속만 건물을 짓고 채집하고, 재화, 1:1 채팅, 친구 알림을 받는다 (알림을 보내는 쪽은 friendRoutes.js)

const BUILD_REFUSED = {
  unknown: '서버가 모르는 건물이라 지을 수 없습니다.',
  full: '이 월드에는 건물을 더 지을 수 없습니다.',
  blocked: '다른 건물과 겹쳐서 지을 수 없습니다.',
  poor: '자원이 부족해 지을 수 없습니다.',
  duplicate: '건물을 짓지 못했습니다. 다시 시도해 주세요.',
};

function startChatServer(port) {
  // 토큰의 유저 { id, nickname }, 없는 토큰이거나 DB 오류면 null (그래도 멀티버스 채팅은 되도록)
  const findUser = token => accounts.findSessionUser(token).catch(err => {
    console.error('[채팅] 로그인 확인 오류:', err.message);
    return null;
  });

  // 접속 중인 친구들에게 알림 (친구 목록의 접속 표시가 바뀌도록)
  const notifyFriends = userId => friends.friendIds(userId)
    .then(ids => ids.forEach(id => presence.send(id, chatProtocol.friends())))
    .catch(err => console.error('[채팅] 친구 알림 오류:', err.message));

  // 같은 월드의 건물 변경은 한 번에 하나씩 처리 (동시에 같은 칸에 짓는 것을 막음)
  const worldQueues = new Map(); // 월드 id -> 마지막으로 예약된 작업
  function inWorldOrder(world, task) {
    const run = (worldQueues.get(world) ?? Promise.resolve()).then(task);
    const done = run.catch(() => {});
    worldQueues.set(world, done);
    done.then(() => {
      if (worldQueues.get(world) === done) worldQueues.delete(world);
    });
    return run;
  }

  const canWrite = client => !client.socket.destroyed;

  // 지금 가진 재화를 알려 줌
  async function sendResources(client) {
    const amounts = await resources.get(client.userId);
    if (amounts && canWrite(client)) client.socket.write(chatProtocol.resources(amounts));
  }

  // 건물 생산을 지금까지 정산. 생산된 자원 { 자원: 수량 } 을 돌려주고, 다음 생산 시각을 기억해 둔다
  //   joined: 방금 입장했으면 true. 오래 접속하지 않았으면 그동안 생산한 양을 따로 알려 준다
  async function settleProduction(client, joined = false) {
    const now = Date.now();
    const { gained, nextAt, since } = await production.settle(client.userId, now);
    client.nextProductionAt = nextAt;
    const idle = since === null ? 0 : Math.floor((now - since) / 1000);
    if (joined && idle >= OFFLINE_NOTICE_SECONDS && canWrite(client)) {
      client.socket.write(chatProtocol.offline(idle, gained));
    }
    return gained;
  }

  // 요청을 거절: 이유를 알려 주고, 미리 바꿔 둔 화면을 서버 상태로 되돌리게 건물 목록과 재화를 다시 보낸다
  async function refuse(client, text) {
    if (!canWrite(client)) return;
    client.socket.write(chatProtocol.notice(text));
    client.socket.write(chatProtocol.buildingList(await buildings.list(client.world)));
    if (client.userId !== null) await sendResources(client);
  }

  // 채집 타이머 상태를 알려 줌 (진행 중이면 남은 시간, 끝났으면 0, 없으면 장소가 '')
  function sendExpedition(client, expedition) {
    if (!canWrite(client)) return;
    const remaining = expedition ? Math.max(0, Math.ceil((expedition.endAt - Date.now()) / 1000)) : 0;
    client.socket.write(chatProtocol.expedition(expedition ? expedition.area : '', remaining));
  }

  // 받은 것을 한 줄로 (보상 안내용): '골드 +100, 작은 나무 x1'
  function rewardText(reward) {
    const parts = [];
    if (reward.gold > 0) parts.push(`골드 +${reward.gold}`);
    if (reward.decoration) parts.push(`${catalog.get(reward.decoration)?.name ?? reward.decoration} x1`);
    return parts.join(', ') || '없음';
  }

  // 접속 중인 유저의 건물 생산: 다음 생산 시각이 된 유저만 정산해서 늘어난 재화를 알려 준다
  setInterval(() => {
    const now = Date.now();
    for (const client of presence.users()) {
      if (client.settling || client.nextProductionAt === null || client.nextProductionAt > now) continue;
      client.settling = true;
      settleProduction(client)
        .then(gained => (Object.keys(gained).length ? sendResources(client) : null))
        .catch(err => console.error('[생산] 오류:', err.message))
        .finally(() => {
          client.settling = false;
        });
    }
  }, PRODUCTION_TICK_MS);

  const server = net.createServer(socket => {
    let client = null; // 입장하면 { socket, name, userId, world, zone, nextProductionAt, settling }
    let buffer = '';
    let queue = Promise.resolve(); // 받은 줄을 순서대로 처리 (DB 를 기다리는 동안 다음 줄이 앞서지 않게)
    socket.setEncoding('utf8');
    socket.setNoDelay(true);
    socket.setKeepAlive(true, 10000); // 응답 없는 연결 정리

    socket.on('data', chunk => {
      buffer += chunk;
      let i;
      while ((i = buffer.indexOf('\n')) >= 0) {
        const line = buffer.slice(0, i);
        buffer = buffer.slice(i + 1);
        queue = queue.then(() => handle(line)).catch(err => console.error('[채팅] 오류:', err.message));
      }
      if (buffer.length > MAX_LINE_LENGTH) socket.destroy(); // 줄바꿈 없이 너무 긴 데이터
    });

    socket.on('close', () => {
      if (!client) return;
      // 같은 사용자명/계정의 새 접속이 자리를 차지한 뒤면 떠난 것이 아니므로 알리지 않는다
      const named = presence.byName(client.name) === client;
      const online = client.userId !== null && presence.byUser(client.userId) === client;
      presence.remove(client);
      if (named) {
        console.log(`[채팅] ${client.name} 퇴장`);
        if (client.world !== null) worlds.leaveView(client);
      }
      if (online) notifyFriends(client.userId);
    });
    socket.on('error', () => {}); // 정리는 close에서

    async function handle(line) {
      const msg = chatProtocol.parse(line);
      if (!msg) return;

      if (msg.t === 'join' && !client) {
        const user = msg.token ? await findUser(msg.token) : null;
        if (socket.destroyed) return; // 확인하는 사이에 끊김

        const name = user ? user.nickname : msg.name;
        const old = presence.byName(name);
        if (old && old.userId !== null && !user) {
          socket.end(chatProtocol.kick()); // 로그인한 사람의 이름을 토큰 없이 쓸 수는 없다
          return;
        }
        if (old) {
          worlds.disconnect(old); // 같은 계정(사용자명)의 기존 접속 끊기: 그쪽 클라이언트는 로그아웃된다
          console.log(`[채팅] ${name} 중복 접속 - 기존 접속 종료`);
        }
        client = { socket, name, userId: user ? user.id : null, world: null, zone: '', nextProductionAt: null, settling: false };
        presence.add(client);
        console.log(`[채팅] ${name} 입장`);

        // 요청한 월드로 (없거나 들어갈 수 없으면 멀티버스)
        if (!await worlds.enter(client, msg.world ?? MULTIVERSE)) await worlds.enter(client, MULTIVERSE);
        if (user) {
          notifyFriends(user.id);
          await settleProduction(client, true); // 접속하지 않은 동안의 생산
          await sendResources(client);
          sendExpedition(client, await gathering.currentExpedition(user.id)); // 꺼 둔 동안에도 흘러간 채집 타이머
        }
        return;
      }
      if (!client || presence.byName(client.name) !== client) return; // 입장하지 않았거나 끊긴 접속

      if (msg.t === 'chat') {
        console.log(`[채팅] ${client.name}: ${msg.text}`);
        const data = chatProtocol.chat(client.name, msg.text);
        for (const other of presence.all()) other.socket.write(data);
      } else if (msg.t === 'enter') {
        if (await worlds.enter(client, msg.world)) {
          console.log(`[월드] ${client.name} -> ${msg.world === MULTIVERSE ? '멀티버스' : '마을 #' + msg.world}`);
        } else if (canWrite(client)) {
          socket.write(chatProtocol.notice('들어갈 수 없는 곳입니다. 친구의 마을만 방문할 수 있습니다.'));
        }
      } else if (msg.t === 'zone') {
        worlds.setZone(client, msg.zone);
      } else if (msg.t === 'gather' || msg.t === 'exp_start' || msg.t === 'exp_claim') {
        if (client.userId === null) {
          if (canWrite(client)) socket.write(chatProtocol.notice('로그인해야 채집할 수 있습니다.'));
          return;
        }
        await gather(msg);
      } else if (msg.t === 'build' || msg.t === 'demolish' || msg.t === 'move') {
        if (client.userId === null) return refuse(client, '로그인해야 건물을 지을 수 있습니다.');
        if (client.world !== MULTIVERSE && client.world !== client.userId) {
          return refuse(client, '친구의 마을에서는 구경만 할 수 있습니다.');
        }
        const world = client.world;
        await inWorldOrder(world, () => changeBuilding(world, msg));
      }
    }

    // 채집: 필드의 자원 노드를 캠, 채집 타이머 시작, 보상 수령
    //   화면에는 클라이언트가 먼저 반영해 두므로, 거절할 때도 재화와 타이머 상태를 다시 보내 서버 값으로 맞춘다
    async function gather(msg) {
      const userId = client.userId;

      if (msg.t === 'gather') {
        const result = await gathering.gatherNode(userId, msg.node);
        if (result === 'unknown') socket.write(chatProtocol.notice('서버가 모르는 자원이라 채집되지 않았습니다.'));
        else if (result === 'too_fast') socket.write(chatProtocol.notice('아직 다시 자라지 않은 자원입니다.'));
        else console.log(`[채집] ${client.name} ${msg.node} ${result.item} +${result.amount}`);
        await sendResources(client);
        return;
      }

      if (msg.t === 'exp_start') {
        const result = await gathering.startExpedition(userId, msg.area);
        if (result === 'unknown') {
          socket.write(chatProtocol.notice('서버가 모르는 채집 장소입니다.'));
          sendExpedition(client, await gathering.currentExpedition(userId));
        } else if (result.existing) {
          socket.write(chatProtocol.notice('이미 진행 중인 채집이 있습니다.'));
          sendExpedition(client, result.existing);
        } else {
          console.log(`[채집] ${client.name} 타이머 시작 ${msg.area}`);
          sendExpedition(client, result.started);
        }
        return;
      }

      const result = await gathering.claimExpedition(userId);
      if (!result) {
        socket.write(chatProtocol.notice('받을 채집 보상이 없습니다.'));
        sendExpedition(client, null);
      } else if (result.pending) {
        socket.write(chatProtocol.notice('채집이 아직 끝나지 않았습니다.'));
        sendExpedition(client, result.pending);
      } else {
        console.log(`[채집] ${client.name} 타이머 보상 ${result.reward.area}: ${rewardText(result.reward)}`);
        socket.write(chatProtocol.notice(`채집 보상 (${result.reward.area}): ${rewardText(result.reward)}`));
        sendExpedition(client, null);
      }
      await sendResources(client);
    }

    // 건물 건설, 철거, 이동: DB 와 재화에 반영하고 같은 월드의 다른 사람들에게 전달
    async function changeBuilding(world, msg) {
      const user = { id: client.userId, nickname: client.name };

      if (msg.t === 'build') {
        const result = await buildings.build(world, user, msg);
        if (result !== 'ok') return refuse(client, BUILD_REFUSED[result]);
        console.log(`[건물] ${client.name} 건설 ${msg.type} (${msg.x}, ${msg.y}) 월드 ${world}`);
        worlds.broadcast(world, chatProtocol.build({ ...msg, owner: client.name }), client);
      } else if (msg.t === 'demolish') {
        await settleProduction(client); // 철거하기 전까지 생산한 것은 받고 지운다
        if (!await buildings.demolish(world, user, msg.id)) {
          return refuse(client, '내가 지은 건물만 철거할 수 있습니다.');
        }
        console.log(`[건물] ${client.name} 철거 ${msg.id} 월드 ${world}`);
        worlds.broadcast(world, chatProtocol.demolish(msg.id), client);
      } else {
        const moved = await buildings.move(world, user, msg.id, msg.x, msg.y);
        if (!moved) return refuse(client, '건물을 옮길 수 없습니다.');
        console.log(`[건물] ${client.name} 이동 ${moved.type} -> (${moved.x}, ${moved.y}) 월드 ${world}`);
        worlds.broadcast(world, chatProtocol.move(moved), client);
        return;
      }

      // 건물이 늘거나 줄었으니 다음 생산 시각을 다시 계산하고, 비용/환급이 반영된 재화를 알려 준다
      await settleProduction(client);
      await sendResources(client);
    }
  });

  server.on('error', err => console.error('채팅 서버 오류:', err.message));
  server.listen(port, () => console.log('채팅(TCP) 서버 실행 :' + port));
  return server;
}

module.exports = startChatServer;
