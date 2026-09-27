const { PORT, PRUNE_INTERVAL_MS } = require('./src/config');
const Players = require('./src/players');
const startUdpServer = require('./src/udpServer');
const startWebServer = require('./src/webServer');

const players = new Players();
startUdpServer(PORT, players);
startWebServer(PORT, players);

// 일정 시간 소식 없는 플레이어 정리
setInterval(() => {
  for (const p of players.prune()) console.log(`${p.name} 접속 종료 (응답 없음)`);
}, PRUNE_INTERVAL_MS);
