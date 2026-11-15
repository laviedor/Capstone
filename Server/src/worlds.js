const chatProtocol = require('./chatProtocol');
const presence = require('./presence');
const buildings = require('./buildings');
const friends = require('./friends');
const accounts = require('./accounts');
const { MULTIVERSE, KICK_GRACE_MS } = require('./config');

// 월드 이동
//   월드 0: 멀티버스. 누구나 들어가고, 로그인한 사람은 건물도 짓는다
//   내 유저 id: 내 마을. 나만 짓고, 다른 사람은 보이지 않는다 (구경 온 친구만 보임)
//   친구의 유저 id: 친구 마을. 친구만 들어갈 수 있고 구경만 한다
// 같은 월드에 있는 사람끼리만 건물 변경(chatServer.js)을 주고받고,
// 좌표(udpServer.js)는 그 안에서도 같은 구역(마을 / 필드 / 집 안)에 있는 사람끼리만 주고받는다

// 그 월드에 있는 사람들에게 (except 는 빼고)
function broadcast(world, line, except) {
  for (const client of presence.inWorld(world)) if (client !== except) client.socket.write(line);
}

// client 가 들어갈 수 있는 월드면 { id, name: 마을 주인 닉네임(멀티버스는 ''), mine: 내 마을인지, build: 지을 수 있는지 }
async function resolve(client, world) {
  if (world === MULTIVERSE) return { id: MULTIVERSE, name: '', mine: false, build: client.userId !== null };
  if (client.userId === null) return null; // 로그인하지 않은 접속은 멀티버스만
  if (world === client.userId) return { id: world, name: client.name, mine: true, build: true };

  if (!await friends.areFriends(client.userId, world)) return null;
  const owner = await accounts.findUser(world);
  return owner && { id: world, name: owner.nickname, mine: false, build: false };
}

// client 를 그 월드로 옮기고 월드 정보와 건물 목록을 보낸다. 들어갈 수 없는 월드면 false (있던 곳에 그대로 있음)
async function enter(client, world) {
  const info = await resolve(client, world);
  if (!info) return false;
  const list = await buildings.list(info.id);
  if (client.socket.destroyed) return false; // 확인하는 사이에 끊김

  const old = client.world;
  if (old !== info.id) {
    if (old !== null) leaveView(client); // 있던 곳의 화면에서 바로 사라지도록
    client.world = info.id;
    client.zone = ''; // 다른 월드에는 마을로 들어간다
  }
  client.socket.write(chatProtocol.world(info));
  client.socket.write(chatProtocol.buildingList(list));
  return true;
}

// client 가 보이던 사람들(같은 월드, 같은 구역)에게 떠났다고 알림
function leaveView(client) {
  const line = chatProtocol.left(client.name);
  for (const other of presence.inZone(client.world, client.zone)) if (other !== client) other.socket.write(line);
}

// 월드 안에서 구역을 옮김 (마을 <-> 필드 / 집 안). 있던 구역의 사람들 화면에서는 사라진다
function setZone(client, zone) {
  if (client.world === null || client.zone === zone) return;
  leaveView(client);
  client.zone = zone;
}

// 중복 로그인: 같은 계정(사용자명)이 다른 곳에서 로그인하거나 접속했을 때 먼저 있던 접속을 끊는다
//   kick 을 받은 클라이언트는 로그아웃하고 로그인 화면으로 돌아간다. 같이 보이던 사람들 화면에서는 바로 사라진다
function disconnect(client) {
  if (client.socket.writableEnded) return; // 이미 끊는 중
  client.socket.end(chatProtocol.kick());
  if (client.world !== null) leaveView(client);
  // 응답이 없는 클라이언트가 접속 목록에 남지 않도록, 스스로 닫지 않으면 서버가 닫는다
  setTimeout(() => client.socket.destroy(), KICK_GRACE_MS).unref();
}

// 이 계정의 접속이 있으면 끊고 true (다른 곳에서 그 계정으로 로그인했을 때 auth.js 가 부른다)
function kick(userId) {
  const client = presence.byUser(userId);
  if (!client || client.socket.writableEnded) return false;
  disconnect(client);
  return true;
}

// 그 월드에 있는 사람들에게 건물 목록을 다시 보냄 (서버와 화면이 어긋났을 때 맞추는 용도)
async function resync(world) {
  broadcast(world, chatProtocol.buildingList(await buildings.list(world)));
}

// 마을 주인과 더 이상 친구가 아니게 된 사람이 그 마을에 있으면 자기 마을로 돌려보낸다
async function evict(ownerId, visitorId) {
  const visitor = presence.byUser(visitorId);
  if (!visitor || visitor.world !== ownerId) return;
  visitor.socket.write(chatProtocol.notice('친구가 아니게 되어 내 마을로 돌아왔습니다.'));
  await enter(visitor, visitorId);
}

// 탈퇴한 유저의 마을에 있던 사람들을 자기 마을로 돌려보내고, 멀티버스에서 그 유저의 건물을 지운다
//   (그 유저와 건물이 DB 에서 지워진 뒤에 호출)
async function closeVillage(userId) {
  for (const client of presence.inWorld(userId)) {
    if (client.userId === userId) continue; // 탈퇴한 본인의 접속은 곧 끊긴다
    client.socket.write(chatProtocol.notice('마을 주인이 탈퇴해 내 마을로 돌아왔습니다.'));
    await enter(client, client.userId);
  }
  await resync(MULTIVERSE);
}

module.exports = { broadcast, enter, leaveView, setZone, disconnect, kick, resync, evict, closeVillage };
