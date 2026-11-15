// 채팅 서버(TCP)에 접속해 있는 사람들
//   client: { socket, name, userId, world, zone, ... }
//     name: 사용자명, userId: 로그인 토큰으로 입장했으면 유저 id (아니면 null),
//     world: 지금 있는 월드 id (config.js MULTIVERSE 참고, 아직 들어가기 전이면 null)
//     zone: 그 월드 안의 구역 ('' 는 마을, 'field' 는 필드, 'house' 는 집 안). 같은 구역에 있는 사람끼리만 서로 보인다
//   친구 목록의 접속 표시, 그 사람에게만 보내는 알림(1:1 채팅, 친구, 재화), 월드별 전달에 쓴다
const byName = new Map(); // 사용자명 -> client
const byUser = new Map(); // 유저 id -> client (로그인 토큰으로 입장한 접속만)

module.exports = {
  // 같은 사용자명(계정)의 이전 접속이 있으면 그 자리를 차지한다
  add(client) {
    byName.set(client.name, client);
    if (client.userId !== null) byUser.set(client.userId, client);
  },

  // 이 접속의 등록을 지움 (같은 사용자명/계정의 새 접속이 이미 자리를 차지했으면 그 등록은 그대로 둔다)
  remove(client) {
    if (byName.get(client.name) === client) byName.delete(client.name);
    if (client.userId !== null && byUser.get(client.userId) === client) byUser.delete(client.userId);
  },

  byName: name => byName.get(name) ?? null,
  byUser: userId => byUser.get(userId) ?? null,
  isOnline: userId => byUser.has(userId),

  // 접속 중이면 보내고 true
  send(userId, line) {
    const client = byUser.get(userId);
    if (!client) return false;
    client.socket.write(line);
    return true;
  },

  // 그 사용자명의 접속이 있는 월드 id, 접속해 있지 않으면 null
  worldOfName: name => byName.get(name)?.world ?? null,
  // 그 사용자명의 접속이 있는 구역, 접속해 있지 않으면 '' (마을)
  zoneOfName: name => byName.get(name)?.zone ?? '',

  all: () => [...byName.values()],
  users: () => [...byUser.values()],
  inWorld: world => [...byName.values()].filter(client => client.world === world),
  inZone: (world, zone) => [...byName.values()].filter(client => client.world === world && client.zone === zone),
};
