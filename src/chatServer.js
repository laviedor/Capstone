const net = require('net');
const chatProtocol = require('./chatProtocol');
const { MAX_LINE_LENGTH } = require('./config');

// TCP 서버: 받은 채팅과 건물 변경(건설, 철거, 이동)을 접속자들에게 전달
//   buildings: 지금 맵에 있는 건물 (buildings.js), 입장하는 사람에게 목록을 보내 줌
function startChatServer(port, buildings) {
  const clients = new Map(); // 사용자명 -> socket

  // except 를 뺀 접속자 모두에게
  const broadcast = (data, except) => {
    for (const s of clients.values()) if (s !== except) s.write(data);
  };

  const server = net.createServer(socket => {
    let name = null;
    let buffer = '';
    socket.setEncoding('utf8');
    socket.setNoDelay(true);
    socket.setKeepAlive(true, 10000); // 응답 없는 연결 정리

    socket.on('data', chunk => {
      buffer += chunk;
      let i;
      while ((i = buffer.indexOf('\n')) >= 0) {
        handle(buffer.slice(0, i));
        buffer = buffer.slice(i + 1);
      }
      if (buffer.length > MAX_LINE_LENGTH) socket.destroy(); // 줄바꿈 없이 너무 긴 데이터
    });

    socket.on('close', () => {
      if (name && clients.get(name) === socket) {
        clients.delete(name);
        console.log(`[채팅] ${name} 퇴장`);
      }
    });
    socket.on('error', () => {}); // 정리는 close에서

    function handle(line) {
      const msg = chatProtocol.parse(line);
      if (!msg) return;

      if (msg.t === 'join' && !name) {
        const old = clients.get(msg.name);
        if (old) {
          old.end(chatProtocol.kick()); // 같은 사용자명의 기존 접속 끊기
          console.log(`[채팅] ${msg.name} 중복 접속 - 기존 접속 종료`);
        }
        name = msg.name;
        clients.set(name, socket);
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
