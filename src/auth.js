const crypto = require('crypto');
const express = require('express');
const steam = require('./steam');
const accounts = require('./accounts');
const { PUBLIC_URL, LOGIN_TTL_MS, MAX_PENDING_LOGINS } = require('./config');

// Steam 로그인 API (HTTP, JSON)
//   POST /auth/steam/start                    -> { loginKey, url }
//          url 을 브라우저로 열어 Steam 로그인. loginKey 는 클라이언트만 갖고 있는다
//   GET  /auth/steam/callback                 Steam 로그인 후 브라우저가 돌아오는 곳 (클라이언트가 부르지 않음)
//   POST /auth/steam/poll  { loginKey }       1~2초마다 호출해 결과 확인
//          -> { status: 'pending' }                                아직 로그인 중
//             { status: 'ok', token, user: { id, nickname }, isNew } 로그인 완료, 이 응답은 한 번만 옴
//             { status: 'failed' }                                 Steam 인증 실패
//             404 { status: 'expired' }                            없는 요청이거나 시간 초과
//   GET  /auth/me  Authorization: Bearer 토큰 -> { id, nickname } / 401
//
// token 은 클라이언트에 저장해 두고, 다음 실행 때 /auth/me 가 성공하면 Steam 로그인을 건너뛴다

const CALLBACK_PATH = '/auth/steam/callback';

// 진행 중인 로그인: state -> { status: 'waiting' | 'verifying' | 'done' | 'failed', expiresAt, user, isNew }
//   state = sha256(loginKey). 브라우저 주소에는 state 만 드러나므로
//   주소가 새어 나가도 loginKey 를 가진 클라이언트만 결과(토큰)를 받을 수 있다
const logins = new Map();
const stateOf = loginKey => crypto.createHash('sha256').update(loginKey).digest('hex');
const returnToOf = state => `${PUBLIC_URL}${CALLBACK_PATH}?state=${state}`;

// Express 4 는 async 핸들러의 오류를 잡지 못하므로 넘겨준다
const wrap = fn => (req, res, next) => fn(req, res).catch(next);

const router = express.Router();

router.post('/auth/steam/start', (req, res) => {
  const now = Date.now();
  for (const [state, login] of logins) if (login.expiresAt < now) logins.delete(state);
  if (logins.size >= MAX_PENDING_LOGINS) return res.status(503).json({ error: 'TOO_MANY_LOGINS' });

  const loginKey = crypto.randomBytes(32).toString('base64url');
  const state = stateOf(loginKey);
  logins.set(state, { status: 'waiting', expiresAt: now + LOGIN_TTL_MS });
  res.json({ loginKey, url: steam.loginUrl(returnToOf(state), PUBLIC_URL + '/') });
});

router.get(CALLBACK_PATH, async (req, res) => {
  const query = new URL(req.originalUrl, PUBLIC_URL).searchParams;
  const state = query.get('state') || '';
  const login = logins.get(state);
  if (!login || login.expiresAt < Date.now() || login.status !== 'waiting') {
    return page(res, 410, '로그인 요청이 만료되었습니다. 게임에서 다시 시도해 주세요.');
  }

  login.status = 'verifying';
  try {
    const steamId = await steam.verify(query, returnToOf(state));
    if (!steamId) {
      login.status = 'failed';
      return page(res, 403, 'Steam 인증에 실패했습니다. 게임에서 다시 시도해 주세요.');
    }

    let user = await accounts.findSteamUser(steamId);
    const isNew = !user;
    if (isNew) user = await accounts.createSteamUser(steamId, await steam.fetchPersonaName(steamId));

    Object.assign(login, { status: 'done', user, isNew });
    console.log(`[로그인] ${user.nickname} (#${user.id}, steam ${steamId})${isNew ? ' 신규 가입' : ''}`);
    page(res, 200, '로그인되었습니다. 게임으로 돌아가 주세요. 이 창은 닫아도 됩니다.');
  } catch (err) {
    login.status = 'failed';
    console.error('[로그인] 오류:', err.message);
    page(res, 500, '서버 오류로 로그인하지 못했습니다. 잠시 후 다시 시도해 주세요.');
  }
});

router.post('/auth/steam/poll', express.json(), wrap(async (req, res) => {
  const loginKey = req.body?.loginKey;
  const state = typeof loginKey === 'string' ? stateOf(loginKey) : '';
  const login = logins.get(state);
  if (!login || login.expiresAt < Date.now()) {
    logins.delete(state);
    return res.status(404).json({ status: 'expired' });
  }
  if (login.status === 'waiting' || login.status === 'verifying') return res.json({ status: 'pending' });

  logins.delete(state); // 결과는 한 번만 전달
  if (login.status === 'failed') return res.json({ status: 'failed' });
  const token = await accounts.createSession(login.user.id);
  res.json({ status: 'ok', token, user: login.user, isNew: login.isNew });
}));

router.get('/auth/me', wrap(async (req, res) => {
  const m = /^Bearer (\S+)$/.exec(req.get('authorization') || '');
  const user = m && await accounts.findSessionUser(m[1]);
  if (!user) return res.status(401).json({ error: 'UNAUTHORIZED' });
  res.json(user);
}));

// 잘못된 JSON(400), DB 오류(500) 등
router.use((err, req, res, next) => {
  const status = err.status || 500;
  if (status >= 500) console.error('[로그인] 오류:', err.message);
  res.status(status).json({ error: status >= 500 ? 'SERVER_ERROR' : 'BAD_REQUEST' });
});

// 로그인 결과를 브라우저에 보여주는 페이지
function page(res, status, message) {
  res.status(status).type('html').send(`<!doctype html><meta charset="utf-8"><title>Steam 로그인</title><p>${message}</p>`);
}

module.exports = router;
