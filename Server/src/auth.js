const crypto = require('crypto');
const express = require('express');
const steam = require('./steam');
const accounts = require('./accounts');
const friends = require('./friends');
const presence = require('./presence');
const chatProtocol = require('./chatProtocol');
const { bearer, sessionUser, wrap, errorHandler } = require('./httpUtil');
const {
  PUBLIC_URL, LOGIN_TTL_MS, MAX_PENDING_LOGINS,
  GUEST_ID_PATTERN, MIN_PASSWORD_LENGTH, MAX_PASSWORD_LENGTH,
  MAX_LOGIN_FAILURES, LOGIN_FAILURE_WINDOW_MS, MAX_SIGNUPS_PER_IP, SIGNUP_WINDOW_MS,
} = require('./config');

// 로그인 API (HTTP, JSON): Steam 로그인, Guest 로그인(아이디 + 비밀번호)
//   POST   /auth/steam/start                  -> { loginKey, url }
//            url 을 브라우저로 열어 Steam 로그인. loginKey 는 클라이언트만 갖고 있는다
//   GET    /auth/steam/callback               Steam 로그인 후 브라우저가 돌아오는 곳 (클라이언트가 부르지 않음)
//   POST   /auth/steam/poll    { loginKey }   1~2초마다 호출해 결과 확인
//            -> { status: 'pending' }                      아직 로그인 중
//               { status: 'ok', token, user: { id, nickname } }  로그인 완료, 이 응답은 한 번만 옴
//               { status: 'unregistered', nickname }       처음 온 Steam 계정. 회원가입 할지 물어본 뒤 signup
//               { status: 'failed' }                       Steam 인증 실패
//               404 { status: 'expired' }                  없는 요청이거나 시간 초과
//   POST   /auth/steam/signup  { loginKey }   회원가입 (poll 이 unregistered 일 때)
//            -> { status: 'ok', token, user: { id, nickname } } / 404 { status: 'expired' }
//   POST   /auth/guest/login  { id, password, signup }   Guest 로그인. 로그인과 회원가입을 같이 처리한다
//            -> { status: 'ok', token, user: { id, nickname } }  로그인 완료 (가입했으면 created: true)
//               { status: 'unregistered' }            없는 아이디. 가입할지 물어본 뒤 signup: true 로 다시 보내면 가입
//               401 { status: 'wrong_password' }      있는 아이디인데 비밀번호가 다름
//               409 { status: 'taken' }               다른 계정이 닉네임으로 쓰고 있어 가입할 수 없는 아이디
//               400 { status: 'invalid' }             아이디, 비밀번호 형식이 맞지 않음 (config.js)
//               429 { status: 'too_many' }            비밀번호를 너무 많이 틀렸거나 가입을 너무 많이 함
//   GET    /auth/me     Authorization: Bearer 토큰 -> { id, nickname } / 401
//   DELETE /auth/me     Authorization: Bearer 토큰 -> { ok: true } / 401   회원 탈퇴
//   POST   /auth/logout Authorization: Bearer 토큰 -> { ok: true }         로그아웃 (이 토큰만 무효화)
//
// token 은 클라이언트에 저장해 두고, 다음 실행 때 /auth/me 가 성공하면 Steam 로그인을 건너뛴다

const CALLBACK_PATH = '/auth/steam/callback';

// 진행 중인 로그인: state -> { status, expiresAt, returnTo, user, steamId, nickname }
//   status: 'waiting' | 'verifying' | 'done' | 'unregistered' | 'failed'
//   state = sha256(loginKey). 브라우저 주소에는 state 만 드러나므로
//   주소가 새어 나가도 loginKey 를 가진 클라이언트만 결과(토큰)를 받을 수 있다
const logins = new Map();
const stateOf = loginKey => crypto.createHash('sha256').update(loginKey).digest('hex');

// Steam 로그인 후 브라우저가 돌아올 이 서버 주소
//   PUBLIC_URL 이 없으면 게임이 접속해 온 주소(Host 헤더, 예: 168.110.121.214:7777)를 그대로 씀
const HOST = /^(?:[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*|\[[0-9A-Fa-f:.]+\])(?::\d{1,5})?$/;
function baseUrlOf(req) {
  if (PUBLIC_URL) return PUBLIC_URL;
  const host = req.get('host') || '';
  return HOST.test(host) ? `${req.protocol}://${host}` : null;
}

// loginKey 로 진행 중인 로그인을 찾음, 없거나 시간이 지났으면 login 은 undefined
function findLogin(loginKey) {
  const state = typeof loginKey === 'string' ? stateOf(loginKey) : '';
  let login = logins.get(state);
  if (login && login.expiresAt < Date.now()) {
    logins.delete(state);
    login = undefined;
  }
  return { state, login };
}

// 횟수 제한: key 마다 windowMs 동안 max 번까지
class Limiter {
  constructor(max, windowMs) {
    this.max = max;
    this.windowMs = windowMs;
    this.hits = new Map(); // key -> { count, resetAt }
  }

  blocked(key, now = Date.now()) {
    const hit = this.hits.get(key);
    if (hit && hit.resetAt <= now) this.hits.delete(key);
    return !!hit && hit.resetAt > now && hit.count >= this.max;
  }

  add(key, now = Date.now()) {
    if (this.hits.size >= 10000) {
      for (const [k, hit] of this.hits) if (hit.resetAt <= now) this.hits.delete(k);
    }
    const hit = this.hits.get(key);
    if (hit && hit.resetAt > now) hit.count++;
    else this.hits.set(key, { count: 1, resetAt: now + this.windowMs });
  }

  clear(key) {
    this.hits.delete(key);
  }
}

const loginFailures = new Limiter(MAX_LOGIN_FAILURES, LOGIN_FAILURE_WINDOW_MS); // 'IP|아이디' 마다 비밀번호 틀린 횟수
const signups = new Limiter(MAX_SIGNUPS_PER_IP, SIGNUP_WINDOW_MS);              // IP 마다 만든 Guest 계정 수

const router = express.Router();

router.post('/auth/steam/start', (req, res) => {
  const now = Date.now();
  for (const [state, login] of logins) if (login.expiresAt < now) logins.delete(state);
  if (logins.size >= MAX_PENDING_LOGINS) return res.status(503).json({ error: 'TOO_MANY_LOGINS' });

  const base = baseUrlOf(req);
  if (!base) return res.status(400).json({ error: 'BAD_HOST' });

  const loginKey = crypto.randomBytes(32).toString('base64url');
  const state = stateOf(loginKey);
  const returnTo = `${base}${CALLBACK_PATH}?state=${state}`;
  logins.set(state, { status: 'waiting', expiresAt: now + LOGIN_TTL_MS, returnTo });
  res.json({ loginKey, url: steam.loginUrl(returnTo, base + '/') });
});

router.get(CALLBACK_PATH, async (req, res) => {
  const query = new URL(req.originalUrl, 'http://localhost').searchParams; // 쿼리만 읽음
  const state = query.get('state') || '';
  const login = logins.get(state);
  if (!login || login.expiresAt < Date.now() || login.status !== 'waiting') {
    return page(res, 410, '로그인 요청이 만료되었습니다. 게임에서 다시 시도해 주세요.');
  }

  login.status = 'verifying';
  try {
    const steamId = await steam.verify(query, login.returnTo);
    if (!steamId) {
      login.status = 'failed';
      return page(res, 403, 'Steam 인증에 실패했습니다. 게임에서 다시 시도해 주세요.');
    }

    const user = await accounts.findSteamUser(steamId);
    if (user) {
      Object.assign(login, { status: 'done', user });
      console.log(`[로그인] ${user.nickname} (#${user.id}, steam ${steamId})`);
    } else {
      // 처음 온 Steam 계정: 게임에서 회원가입을 확인받은 뒤 /auth/steam/signup 에서 만든다
      const nickname = accounts.toNickname(await steam.fetchPersonaName(steamId), steamId);
      Object.assign(login, { status: 'unregistered', steamId, nickname });
    }
    page(res, 200, 'Steam 확인이 끝났습니다. 게임으로 돌아가 주세요. 이 창은 닫아도 됩니다.');
  } catch (err) {
    login.status = 'failed';
    console.error('[로그인] 오류:', err.message);
    page(res, 500, '서버 오류로 로그인하지 못했습니다. 잠시 후 다시 시도해 주세요.');
  }
});

router.post('/auth/steam/poll', express.json(), wrap(async (req, res) => {
  const { state, login } = findLogin(req.body?.loginKey);
  if (!login) return res.status(404).json({ status: 'expired' });
  if (login.status === 'waiting' || login.status === 'verifying') return res.json({ status: 'pending' });
  if (login.status === 'unregistered') return res.json({ status: 'unregistered', nickname: login.nickname });

  logins.delete(state); // 결과는 한 번만 전달
  if (login.status === 'failed') return res.json({ status: 'failed' });
  const token = await accounts.createSession(login.user.id);
  res.json({ status: 'ok', token, user: login.user });
}));

router.post('/auth/steam/signup', express.json(), wrap(async (req, res) => {
  const { state, login } = findLogin(req.body?.loginKey);
  if (!login || login.status !== 'unregistered') return res.status(404).json({ status: 'expired' });

  logins.delete(state);
  const user = await accounts.createSteamUser(login.steamId, login.nickname);
  const token = await accounts.createSession(user.id);
  console.log(`[회원가입] ${user.nickname} (#${user.id}, steam ${login.steamId})`);
  res.json({ status: 'ok', token, user });
}));

router.post('/auth/guest/login', express.json(), wrap(async (req, res) => {
  const id = typeof req.body?.id === 'string' ? req.body.id.normalize('NFC').trim() : '';
  const password = req.body?.password;
  if (!GUEST_ID_PATTERN.test(id) || typeof password !== 'string'
      || password.length < MIN_PASSWORD_LENGTH || password.length > MAX_PASSWORD_LENGTH) {
    return res.status(400).json({ status: 'invalid' });
  }

  const failKey = `${req.ip}|${id.toLowerCase()}`;
  if (loginFailures.blocked(failKey)) return res.status(429).json({ status: 'too_many' });

  // 있는 아이디: 비밀번호가 맞아야 로그인
  const guest = await accounts.findGuest(id);
  if (guest) {
    if (!await accounts.verifyPassword(password, guest.passwordHash)) {
      loginFailures.add(failKey);
      return res.status(401).json({ status: 'wrong_password' });
    }
    loginFailures.clear(failKey);
    const user = { id: guest.id, nickname: guest.nickname };
    const token = await accounts.createSession(user.id);
    console.log(`[로그인] ${user.nickname} (#${user.id}, guest)`);
    return res.json({ status: 'ok', token, user });
  }

  // 없는 아이디: 회원가입 (게임에서 가입할지 확인받고 signup: true 로 다시 보냄)
  if (await accounts.nicknameTaken(id)) return res.status(409).json({ status: 'taken' });
  if (req.body.signup !== true) return res.json({ status: 'unregistered' });
  if (signups.blocked(req.ip)) return res.status(429).json({ status: 'too_many' });

  const user = await accounts.createGuestUser(id, password);
  if (!user) return res.status(409).json({ status: 'taken' });
  signups.add(req.ip);
  const token = await accounts.createSession(user.id);
  console.log(`[회원가입] ${user.nickname} (#${user.id}, guest)`);
  res.json({ status: 'ok', token, user, created: true });
}));

router.get('/auth/me', wrap(async (req, res) => {
  const user = await sessionUser(req);
  if (!user) return res.status(401).json({ error: 'UNAUTHORIZED' });
  res.json(user);
}));

router.delete('/auth/me', wrap(async (req, res) => {
  const user = await sessionUser(req);
  if (!user) return res.status(401).json({ error: 'UNAUTHORIZED' });
  const friendIds = await friends.friendIds(user.id);
  await accounts.deleteUser(user.id);
  for (const id of friendIds) presence.send(id, chatProtocol.friends()); // 친구들의 목록에서도 사라지도록
  console.log(`[회원 탈퇴] ${user.nickname} (#${user.id})`);
  res.json({ ok: true });
}));

router.post('/auth/logout', wrap(async (req, res) => {
  const token = bearer(req);
  if (token) await accounts.deleteSession(token);
  res.json({ ok: true });
}));

router.use(errorHandler('로그인'));

// 로그인 결과를 브라우저에 보여주는 페이지
function page(res, status, message) {
  res.status(status).type('html').send(`<!doctype html><meta charset="utf-8"><title>Steam 로그인</title><p>${message}</p>`);
}

module.exports = router;
