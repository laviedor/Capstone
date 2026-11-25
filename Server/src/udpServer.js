const dgram = require('dgram');
const protocol = require('./protocol');
const presence = require('./presence');
const positions = require('./positions');
const { MULTIVERSE } = require('./config');

// UDP 게임 통신: 플레이어 좌표를 받아 같은 월드, 같은 구역에 있는 다른 플레이어들에게 전달
//   UDP 는 닿지 않아도 알 수 없으므로, 같은 좌표가 채팅 연결(TCP)로도 오간다 (positions.js). 여기는 자주 오는 부드러운 움직임을 맡는다
function startUdpServer(port, players) {
  const socket = dgram.createSocket('udp4');
  const send = (text, to) => socket.send(text, to.port, to.address);
  const worldOf = name => presence.worldOfName(name) ?? MULTIVERSE;
  const zoneOf = name => presence.zoneOfName(name);
  positions.attachUdp(players, send); // UDP 가 죽은 새 클라이언트의 좌표를 예전 클라이언트에게 UDP 로 전할 때 쓴다

  socket.on('message', (buf, rinfo) => {
    const msg = protocol.parse(buf);
    if (!msg) return;

    // 어느 월드, 어느 구역에 있는지는 채팅 서버(TCP)에서 확인된 것만 믿는다 (들어갈 수 없는 마을의 좌표를 받아 보지 못하게)
    //   채팅 서버에 접속해 있지 않은 사용자명은 광장의 마을 구역만 가능
    if (msg.world !== worldOf(msg.name) || msg.zone !== zoneOf(msg.name)) return;

    const { rejected, player, prev, kicked } = players.update(msg, rinfo);
    if (rejected) {
      send(protocol.kick(), rinfo); // 이미 끊긴 세션이면 다시 알림
      return;
    }

    if (kicked) {
      send(protocol.kick(), kicked);
      console.log(`${player.name} 중복 접속 - 기존 접속 종료`);
    } else if (!prev) {
      console.log(`${player.name} 접속`);
    }

    // 받는 쪽도 지금 그곳에 있을 때만 (방금 다른 곳으로 간 사람은 좌표를 새로 보내기 전까지 기록이 예전 위치로 남아 있다)
    const data = protocol.position(player);
    for (const other of players.others(player.name, player.world, player.zone)) {
      if (worldOf(other.name) === player.world && zoneOf(other.name) === player.zone) send(data, other);
    }

    // 예전 클라이언트의 좌표는 채팅 연결을 아는 사람들에게도 전한다 (그 사람들의 UDP 수신이 막혀 있어도 보이도록)
    positions.fromUdp(player);
  });

  socket.on('error', err => console.error('UDP 오류:', err.message));
  socket.bind(port, () => console.log('UDP 서버 실행 :' + port));
  return socket;
}

module.exports = startUdpServer;
