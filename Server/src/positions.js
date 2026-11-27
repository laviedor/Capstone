const chatProtocol = require('./chatProtocol');
const protocol = require('./protocol');
const presence = require('./presence');
const { POSITION_MIN_INTERVAL_MS, LEGACY_RELAY_INTERVAL_MS, UDP_FRESH_MS } = require('./config');

// 같은 월드, 같은 구역에 있는 사람들의 위치를 채팅 연결(TCP)로도 알려 준다
//   좌표는 원래 UDP(udpServer.js)로만 오갔다. UDP 는 가는 길이나 오는 길이 막혀도 아무도 알 수 없어서,
//   그럴 때는 채팅과 건물은 멀쩡한데 다른 사람만 아예 보이지 않았다
//   그래서 누가 같이 있는지와 그 위치는 끊기지 않는 채팅 연결로도 보낸다. UDP 는 그대로 쓰고 (더 자주 와서 움직임이 부드럽다),
//   채팅 연결로는 드문드문 오지만 반드시 온다: 둘 중 하나만 살아 있어도 서로 보인다
//     들어올 때 (입장, 월드 이동, 구역 이동): 그곳에 있는 사람들의 위치 목록 (players)
//     움직일 때: 클라이언트가 채팅 연결로도 보낸 위치를 같은 곳의 사람들에게 전달 (pos)
//     떠날 때: left (worlds.js)
//   이 메시지를 아는 클라이언트(join 의 v 가 2 이상)에게만 보낸다
//   예전 클라이언트끼리는 지금처럼 UDP 로만 주고받는다. 새 클라이언트와 섞여 있을 때는 여기서 서로 옮겨 전한다:
//     예전 클라이언트가 UDP 로 보낸 좌표 -> 새 클라이언트에게 채팅 연결로 (fromUdp)
//     UDP 가 죽은 새 클라이언트가 채팅 연결로 보낸 좌표 -> 예전 클라이언트에게 UDP 로 (relayToLegacy)
//
//   client.pos: 채팅 연결로 알고 있는 그 사람의 위치 { x, y, z, n } (지금 있는 곳에서 아직 받은 적이 없으면 null)
//   client.look: 그 사람의 겉모습 { hat, top, bottom, tool } (착용한 장비 id, 보낸 적이 없으면 null)
//     들어올 때 받는 목록(players)에 같이 들어가고, 바꾸거나 다른 곳에 들어가면 그곳 사람들에게 look 으로 알린다

const knows = client => client.v >= 2; // players, pos 메시지를 아는 클라이언트인지

let udp = null; // UDP 쪽 { players: 좌표 기록(players.js), send(text, to) } (udpServer.js 가 알려 준다)

function attachUdp(players, send) {
  udp = { players, send };
}

const info = client => ({
  name: client.name, id: client.userId ?? 0, x: client.pos.x, y: client.pos.y, z: client.pos.z, n: client.pos.n,
  look: client.look ?? undefined,
});

// client 와 같은 곳에 있는 사람들 중 이 메시지를 아는 클라이언트에게
function tell(client, line) {
  for (const other of presence.inZone(client.world, client.zone)) {
    if (other !== client && knows(other) && !other.socket.destroyed) other.socket.write(line);
  }
}

// client 가 지금 있는 곳의 사람들 위치를 client 에게 보낸다 (입장, 월드 이동, 구역 이동 직후)
//   받은 클라이언트는 이 목록에 맞춰 화면의 사람들을 다시 맞추고, 자기 위치를 곧바로 보낸다
function snapshot(client) {
  if (!knows(client) || client.world === null || client.socket.destroyed) return;
  const list = presence.inZone(client.world, client.zone).filter(other => other !== client && other.pos).map(info);
  client.socket.write(chatProtocol.players(list));
}

// client 가 채팅 연결로 보낸 위치를 기억하고 같은 곳의 사람들에게 알린다
function update(client, pos, now = Date.now()) {
  if (client.world === null) return;
  if (client.posAt && now - client.posAt < POSITION_MIN_INTERVAL_MS) return; // 너무 잦은 것은 버린다
  client.posAt = now;
  client.pos = { x: pos.x, y: pos.y, z: pos.z, n: pos.n };
  tell(client, chatProtocol.position(info(client)));
  relayToLegacy(client, now);
}

// 예전 클라이언트는 채팅 연결로 온 좌표를 모르므로 UDP 로 전한다
//   보낸 사람의 UDP 가 살아 있으면 udpServer.js 가 이미 전하고 있으므로, UDP 소식이 끊긴 사람의 것만 전한다
function relayToLegacy(client, now) {
  if (!udp) return;
  const own = udp.players.byName.get(client.name);
  if (own && own.world === client.world && own.zone === client.zone && now - own.lastSeen < UDP_FRESH_MS) return;

  let data = null;
  for (const other of udp.players.others(client.name, client.world, client.zone)) {
    const peer = presence.byName(other.name);
    if (!peer || knows(peer) || peer.world !== client.world || peer.zone !== client.zone) continue;
    data ??= protocol.position({ name: client.name, x: client.pos.x, y: client.pos.y, z: client.pos.z, n: client.pos.n });
    udp.send(data, other);
  }
}

// 다른 월드나 구역으로 갔으면 예전 위치는 버린다 (그곳의 좌표가 아니다)
function forget(client) {
  client.pos = null;
  client.posAt = 0;
}

// 예전 클라이언트가 UDP 로 보낸 좌표 (udpServer.js 가 받아서 전달한 뒤에 부른다): 새 클라이언트에게 채팅 연결로도 전한다
//   player: players.js 의 기록 { name, world, zone, x, y, z, n }
function fromUdp(player, now = Date.now()) {
  const client = presence.byName(player.name);
  if (!client || knows(client)) return; // 새 클라이언트는 스스로 채팅 연결로도 보낸다
  if (client.world !== player.world || client.zone !== player.zone) return;
  if (client.posAt && now - client.posAt < LEGACY_RELAY_INTERVAL_MS) return;
  client.posAt = now;
  client.pos = { x: player.x, y: player.y, z: player.z, n: player.n ?? 0 };
  tell(client, chatProtocol.position(info(client)));
}

// 예전 클라이언트의 UDP 가 끊겼을 때 (server.js 의 정리): 채팅 연결이 남아 있어도 화면에서는 사라지게 한다
//   (예전 클라이언트는 ping 을 보내지 않아서 UDP 소식이 끊긴 것이 그 사람이 사라졌다는 유일한 신호다. 다시 오면 다시 나타난다)
function udpLost(name) {
  const client = presence.byName(name);
  if (!client || knows(client) || !client.pos || client.world === null) return;
  forget(client);
  tell(client, chatProtocol.left(client.name));
}

// client 의 겉모습을 같은 곳의 사람들에게 알린다 (장비를 바꿨을 때, 다른 월드·구역에 들어갔을 때)
function announceLook(client) {
  if (!client.look || client.world === null) return;
  tell(client, chatProtocol.look(client.name, client.look));
}

// 겉모습이 바뀌었다고 받았을 때
function setLook(client, look) {
  client.look = look;
  announceLook(client);
}

module.exports = { attachUdp, snapshot, update, forget, fromUdp, udpLost, announceLook, setLook };
