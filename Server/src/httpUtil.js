const accounts = require('./accounts');

// HTTP API (auth.js, friendRoutes.js) 에서 같이 쓰는 것들

// Authorization: Bearer 토큰, 없으면 null
function bearer(req) {
  const m = /^Bearer (\S+)$/.exec(req.get('authorization') || '');
  return m ? m[1] : null;
}

// 토큰의 유저 { id, nickname }, 없으면 null
async function sessionUser(req) {
  const token = bearer(req);
  return token ? accounts.findSessionUser(token) : null;
}

// Express 4 는 async 핸들러의 오류를 잡지 못하므로 넘겨준다
const wrap = fn => (req, res, next) => fn(req, res, next).catch(next);

// 잘못된 JSON(400), DB 오류(500) 등. label 은 로그 앞에 붙는 이름
const errorHandler = label => (err, req, res, next) => {
  const status = err.status || 500;
  if (status >= 500) console.error(`[${label}] 오류:`, err.message);
  res.status(status).json({ error: status >= 500 ? 'SERVER_ERROR' : 'BAD_REQUEST' });
};

module.exports = { bearer, sessionUser, wrap, errorHandler };
