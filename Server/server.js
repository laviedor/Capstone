const { PORT, CHAT_PORT, PRUNE_INTERVAL_MS } = require('./src/config');
const Players = require('./src/players');
const startUdpServer = require('./src/udpServer');
const startWebServer = require('./src/webServer');
const startChatServer = require('./src/chatServer');
const db = require('./src/db');

const players = new Players();
startUdpServer(PORT, players);
startWebServer(PORT, players);
startChatServer(CHAT_PORT);

// 로그인, 건물, 재화는 DB 에 있으므로 연결이 안 되면 쓸 수 없지만, 좌표 전달과 채팅은 계속 된다
db.query('SELECT 1')
  .then(() => console.log('DB 연결 OK'))
  .catch(err => console.error('DB 연결 실패 (로그인, 건물, 재화 사용 불가):', err.message || err.code));

// 일정 시간 소식 없는 플레이어 정리
setInterval(() => {
  for (const p of players.prune()) console.log(`${p.name} 접속 종료 (응답 없음)`);
}, PRUNE_INTERVAL_MS);
