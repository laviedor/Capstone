const path = require('path');
const express = require('express');

// 웹 확인 페이지 (public/index.html) + 현재 좌표 API
function startWebServer(port, players) {
  const app = express();
  app.use(express.static(path.join(__dirname, '..', 'public')));
  app.get('/positions', (req, res) => res.json(players.toJSON()));
  return app.listen(port, () => console.log('웹 확인 http://localhost:' + port));
}

module.exports = startWebServer;
