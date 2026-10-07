const crypto = require('crypto');
const pool = require('./db');
const { SESSION_TTL_DAYS, MAX_NICKNAME_LENGTH } = require('./config');

// 계정, 로그인 세션 DB 처리
// 유저는 { id, nickname } 형태로 주고받는다

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
  const nickname = toNickname(name, steamId);
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
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

// 회원 탈퇴: 유저를 지우면 마을, 건물, 인벤토리, 탐사, 좋아요, 방명록, 로그인 연결, 세션이 같이 지워진다 (ON DELETE CASCADE)
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

// 세션 토큰의 유저, 없거나 만료됐으면 null
async function findSessionUser(token) {
  const [rows] = await pool.query(
    `SELECT u.id, u.nickname FROM sessions s JOIN users u ON u.id = s.user_id
      WHERE s.token_hash = ? AND s.expires_at > UTC_TIMESTAMP(3)`,
    [hash(token)],
  );
  return rows[0] ?? null;
}

module.exports = { findSteamUser, createSteamUser, deleteUser, toNickname, createSession, findSessionUser };
