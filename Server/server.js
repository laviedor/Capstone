const { PORT, CHAT_PORT, PRUNE_INTERVAL_MS } = require('./src/config');
const Players = require('./src/players');
const startUdpServer = require('./src/udpServer');
const startWebServer = require('./src/webServer');
const startChatServer = require('./src/chatServer');
const positions = require('./src/positions');
const db = require('./src/db');

// 로그 줄마다 시각을 붙인다 (문제가 생겼을 때 무슨 일이 언제, 어떤 순서로 있었는지 볼 수 있게)
for (const level of ['log', 'error']) {
  const write = console[level].bind(console);
  console[level] = (...args) => write(new Date().toLocaleString('sv-SE', { timeZone: 'Asia/Seoul' }), ...args);
}

const players = new Players();
startUdpServer(PORT, players);
startWebServer(PORT, players);
startChatServer(CHAT_PORT);

// 로그인, 건물, 재화는 DB 에 있으므로 연결이 안 되면 쓸 수 없지만, 좌표 전달과 채팅은 계속 된다
db.query('SELECT 1')
  .then(() => console.log('DB 연결 OK'))
  .catch(err => console.error('DB 연결 실패 (로그인, 건물, 재화 사용 불가):', err.message || err.code));

// 일정 시간 UDP 소식이 없는 플레이어 정리
setInterval(() => {
  for (const p of players.prune()) {
    console.log(`${p.name} 접속 종료 (응답 없음)`);
    positions.udpLost(p.name); // 예전 클라이언트면 채팅 연결로 보던 사람들의 화면에서도 사라지게 한다
  }
}, PRUNE_INTERVAL_MS);
