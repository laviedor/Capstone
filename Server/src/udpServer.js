const dgram = require('dgram');
const protocol = require('./protocol');

// UDP 게임 통신: 플레이어 좌표를 받아 다른 플레이어들에게 전달
function startUdpServer(port, players) {
  const socket = dgram.createSocket('udp4');
  const send = (text, to) => socket.send(text, to.port, to.address);

  socket.on('message', (buf, rinfo) => {
    const msg = protocol.parse(buf);
    if (!msg) return;

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
    if (!prev || prev.x !== player.x || prev.y !== player.y || prev.z !== player.z) {
      console.log(player.name, { x: player.x, y: player.y, z: player.z });
    }

    const data = protocol.position(player);
    for (const other of players.others(player.name)) send(data, other);
  });

  socket.on('error', err => console.error('UDP 오류:', err.message));
  socket.bind(port, () => console.log('UDP 서버 실행 :' + port));
  return socket;
}

module.exports = startUdpServer;
