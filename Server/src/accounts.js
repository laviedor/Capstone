const crypto = require('crypto');
const util = require('util');
const pool = require('./db');
const { SESSION_TTL_DAYS, MAX_NICKNAME_LENGTH } = require('./config');

const scrypt = util.promisify(crypto.scrypt);

// 계정, 로그인 세션 DB 처리
// 유저는 { id, nickname } 형태로 주고받는다
// 닉네임은 유저마다 다르게 만든다 (친구 추가를 닉네임으로 하고, 채팅 서버도 사용자명으로 접속을 구분하므로)

// Steam 계정에 연결된 유저, 없으면 null
async function findSteamUser(steamId) {
  const [rows] = await pool.query(
    `SELECT u.id, u.nickname FROM user_auth a JOIN users u ON u.id = a.user_id
      WHERE a.provider = 'steam' AND a.login_key = ?`,
    [steamId],
  );
  return rows[0] ?? null;
}

// 회원가입: Steam 계정으로 유저와 마을을 새로 만든다
//   name: 닉네임으로 쓸 이름 (Steam 프로필 이름 등, toNickname 으로 정리해서 저장)
async function createSteamUser(steamId, name) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const nickname = await freeNickname(conn, toNickname(name, steamId));
    const [result] = await conn.query('INSERT INTO users (nickname) VALUES (?)', [nickname]);
    const id = result.insertId;
    await conn.query('INSERT INTO villages (owner_id) VALUES (?)', [id]);
    await conn.query("INSERT INTO user_auth (provider, login_key, user_id) VALUES ('steam', ?, ?)", [steamId, id]);
    await conn.commit();
    return { id, nickname };
  } catch (err) {
    await conn.rollback();
    // 같은 Steam 계정으로 동시에 처음 로그인한 경우: 먼저 만들어진 유저를 사용
    if (err.code === 'ER_DUP_ENTRY') {
      const user = await findSteamUser(steamId);
      if (user) return user;
    }
    throw err;
  } finally {
    conn.release();
  }
}

// Guest 계정(아이디 + 비밀번호)의 유저 { id, nickname, passwordHash }, 없는 아이디면 null
async function findGuest(loginId) {
  const [rows] = await pool.query(
    `SELECT u.id, u.nickname, a.password_hash AS passwordHash FROM user_auth a JOIN users u ON u.id = a.user_id
      WHERE a.provider = 'guest' AND a.login_key = ?`,
    [loginId.toLowerCase()],
  );
  return rows[0] ?? null;
}

// 회원가입: Guest 계정으로 유저와 마을을 새로 만든다. 아이디를 그대로 닉네임으로 쓴다
//   그 사이 같은 아이디로 가입됐거나 다른 유저가 닉네임으로 쓰고 있으면 null
async function createGuestUser(loginId, password) {
  const passwordHash = await hashPassword(password);
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    if (await nicknameTaken(loginId, conn)) {
      await conn.rollback();
      return null;
    }
    const [result] = await conn.query('INSERT INTO users (nickname) VALUES (?)', [loginId]);
    const id = result.insertId;
    await conn.query('INSERT INTO villages (owner_id) VALUES (?)', [id]);
    await conn.query(
      "INSERT INTO user_auth (provider, login_key, password_hash, user_id) VALUES ('guest', ?, ?, ?)",
      [loginId.toLowerCase(), passwordHash, id],
    );
    await conn.commit();
    return { id, nickname: loginId };
  } catch (err) {
    await conn.rollback();
    if (err.code === 'ER_DUP_ENTRY') return null; // 같은 아이디로 동시에 가입
    throw err;
  } finally {
    conn.release();
  }
}

// 비밀번호는 원문 대신 scrypt 해시로 저장: 'scrypt:솔트:해시' (16진수)
async function hashPassword(password) {
  const salt = crypto.randomBytes(16);
  const key = await scrypt(password, salt, 32);
  return `scrypt:${salt.toString('hex')}:${key.toString('hex')}`;
}

async function verifyPassword(password, passwordHash) {
  const [scheme, salt, key] = (passwordHash || '').split(':');
  if (scheme !== 'scrypt' || !salt || !key) return false;
  const expected = Buffer.from(key, 'hex');
  const actual = await scrypt(password, Buffer.from(salt, 'hex'), expected.length);
  return crypto.timingSafeEqual(actual, expected);
}

// 이 닉네임을 쓰는 유저가 있는지 (DB 비교 규칙대로 대소문자는 구분하지 않음)
async function nicknameTaken(nickname, conn = pool) {
  const [rows] = await conn.query('SELECT 1 FROM users WHERE nickname = ? LIMIT 1', [nickname]);
  return rows.length > 0;
}

// 이미 쓰는 닉네임이면 뒤에 숫자를 붙여 겹치지 않게 만든다 (Steam 프로필 이름은 서로 같을 수 있음)
async function freeNickname(conn, base) {
  for (let n = 1; n <= 100; n++) {
    const suffix = n === 1 ? '' : String(n);
    const nickname = [...base].slice(0, MAX_NICKNAME_LENGTH - suffix.length).join('') + suffix;
    if (!await nicknameTaken(nickname, conn)) return nickname;
  }
  return base;
}

// 회원 탈퇴: 유저를 지우면 마을, 건물, 인벤토리, 탐사, 좋아요, 방명록, 로그인 연결, 세션, 친구, 1:1 채팅이 같이 지워진다 (ON DELETE CASCADE)
async function deleteUser(userId) {
  await pool.query('DELETE FROM users WHERE id = ?', [userId]);
}

// DB 닉네임 칸에 맞게 정리, 쓸 수 있는 글자가 없으면 기본 닉네임
//   글자 수는 MySQL 과 같게 유니코드 문자 단위로 센다 (이모지도 1자)
function toNickname(name, steamId) {
  const chars = [...(name || '').replace(/\p{Cc}/gu, '').trim()];
  return chars.slice(0, MAX_NICKNAME_LENGTH).join('').trim() || `주민${steamId.slice(-4)}`;
}

const hash = token => crypto.createHash('sha256').update(token).digest('hex');

// 로그인 처리: 세션 토큰을 새로 발급하고 마지막 접속 시각을 갱신
async function createSession(userId) {
  const token = crypto.randomBytes(32).toString('base64url');
  await pool.query('DELETE FROM sessions WHERE user_id = ? AND expires_at <= UTC_TIMESTAMP(3)', [userId]);
  await pool.query(
    'INSERT INTO sessions (token_hash, user_id, expires_at) VALUES (?, ?, UTC_TIMESTAMP(3) + INTERVAL ? DAY)',
    [hash(token), userId, SESSION_TTL_DAYS],
  );
  await pool.query('UPDATE users SET last_seen_at = UTC_TIMESTAMP(3) WHERE id = ?', [userId]);
  return token;
}

// 로그아웃: 이 세션 토큰만 무효화 (다른 기기의 로그인은 유지)
async function deleteSession(token) {
  await pool.query('DELETE FROM sessions WHERE token_hash = ?', [hash(token)]);
}

// 세션 토큰의 유저, 없거나 만료됐으면 null
async function findSessionUser(token) {
  const [rows] = await pool.query(
    `SELECT u.id, u.nickname FROM sessions s JOIN users u ON u.id = s.user_id
      WHERE s.token_hash = ? AND s.expires_at > UTC_TIMESTAMP(3)`,
    [hash(token)],
  );
  return rows[0] ?? null;
}

module.exports = {
  findSteamUser, createSteamUser, findGuest, createGuestUser, verifyPassword, nicknameTaken,
  deleteUser, toNickname, createSession, deleteSession, findSessionUser,
};
