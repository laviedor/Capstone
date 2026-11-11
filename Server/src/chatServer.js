const net = require('net');
const chatProtocol = require('./chatProtocol');
const accounts = require('./accounts');
const friends = require('./friends');
const presence = require('./presence');
const { MAX_LINE_LENGTH } = require('./config');

// TCP 서버: 받은 채팅과 건물 변경(건설, 철거, 이동)을 접속자들에게 전달
//   buildings: 지금 맵에 있는 건물 (buildings.js), 입장하는 사람에게 목록을 보내 줌
//   로그인 토큰으로 입장한 접속은 presence 에 등록되어 1:1 채팅, 친구 알림도 받는다 (보내는 쪽은 friendRoutes.js)
function startChatServer(port, buildings) {
  const clients = new Map(); // 사용자명 -> socket

  // except 를 뺀 접속자 모두에게
  const broadcast = (data, except) => {
    for (const s of clients.values()) if (s !== except) s.write(data);
  };

  // 토큰의 유저 { id, nickname }, 없는 토큰이거나 DB 오류면 null (그래도 광장 채팅은 되도록)
  const findUser = token => accounts.findSessionUser(token).catch(err => {
    console.error('[채팅] 로그인 확인 오류:', err.message);
    return null;
  });

  // 접속 중인 친구들에게 알림 (친구 목록의 접속 표시가 바뀌도록)
  const notifyFriends = userId => friends.friendIds(userId)
    .then(ids => ids.forEach(id => presence.send(id, chatProtocol.friends())))
    .catch(err => console.error('[채팅] 친구 알림 오류:', err.message));

  const server = net.createServer(socket => {
    let name = null;
    let userId = null; // 로그인 토큰으로 입장했으면 유저 id
    let buffer = '';
    let queue = Promise.resolve(); // 받은 줄을 순서대로 처리 (입장할 때 DB 확인을 기다리는 동안 온 줄이 앞서지 않게)
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
      if (name && clients.get(name) === socket) {
        clients.delete(name);
        console.log(`[채팅] ${name} 퇴장`);
      }
      if (userId !== null && presence.remove(userId, socket)) notifyFriends(userId);
    });
    socket.on('error', () => {}); // 정리는 close에서

    async function handle(line) {
      const msg = chatProtocol.parse(line);
      if (!msg) return;

      if (msg.t === 'join' && !name) {
        const user = msg.token ? await findUser(msg.token) : null;
        if (socket.destroyed) return; // 확인하는 사이에 끊김

        const joinName = user ? user.nickname : msg.name;
        const old = clients.get(joinName);
        if (old) {
          old.end(chatProtocol.kick()); // 같은 사용자명의 기존 접속 끊기
          console.log(`[채팅] ${joinName} 중복 접속 - 기존 접속 종료`);
        }
        name = joinName;
        clients.set(name, socket);
        if (user) {
          userId = user.id;
          presence.add(userId, socket);
          notifyFriends(userId);
        }
        console.log(`[채팅] ${name} 입장`);
        socket.write(chatProtocol.buildingList(buildings.list())); // 지금 맵에 있는 건물
        return;
      }
      if (!name || clients.get(name) !== socket) return; // 입장하지 않았거나 끊긴 접속

      if (msg.t === 'chat') {
        console.log(`[채팅] ${name}: ${msg.text}`);
        broadcast(chatProtocol.chat(name, msg.text));
      } else if (msg.t === 'build') {
        const b = { id: msg.id, type: msg.type, x: msg.x, y: msg.y, owner: name };
        if (!buildings.add(b)) return;
        console.log(`[건물] ${name} 건설 ${b.type} (${b.x}, ${b.y})`);
        broadcast(chatProtocol.build(b), socket);
      } else if (msg.t === 'demolish') {
        if (!buildings.remove(msg.id)) return;
        console.log(`[건물] ${name} 철거 ${msg.id}`);
        broadcast(chatProtocol.demolish(msg.id), socket);
      } else if (msg.t === 'move') {
        const b = buildings.move(msg.id, msg.x, msg.y);
        if (!b) return;
        console.log(`[건물] ${name} 이동 ${b.type} -> (${b.x}, ${b.y})`);
        broadcast(chatProtocol.move(b), socket);
      }
    }
  });

  server.on('error', err => console.error('채팅 서버 오류:', err.message));
  server.listen(port, () => console.log('채팅(TCP) 서버 실행 :' + port));
  return server;
}

module.exports = startChatServer;
