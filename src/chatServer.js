const net = require('net');
const chatProtocol = require('./chatProtocol');
const { MAX_LINE_LENGTH } = require('./config');

// TCP 채팅 서버: 받은 채팅을 접속자 모두에게 전달
function startChatServer(port) {
  const clients = new Map(); // 사용자명 -> socket

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
      } else if (msg.t === 'chat' && name && clients.get(name) === socket) {
        console.log(`[채팅] ${name}: ${msg.text}`);
        const data = chatProtocol.chat(name, msg.text);
        for (const s of clients.values()) s.write(data);
      }
    }
  });

  server.on('error', err => console.error('채팅 서버 오류:', err.message));
  server.listen(port, () => console.log('채팅(TCP) 서버 실행 :' + port));
  return server;
}

module.exports = startChatServer;
