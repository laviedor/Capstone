const pool = require('./db');
const { MAX_FRIENDS, DM_PAGE_SIZE } = require('./config');

// 친구, 1:1 채팅 DB 처리
//   친구는 서로 친구: friends 에 양쪽 줄(A->B, B->A)을 같이 넣고 같이 지운다

// 친구 목록 [{ id, nickname, unread }], unread 는 그 친구가 보낸 1:1 채팅 중 아직 안 읽은 개수
async function list(userId) {
  const [rows] = await pool.query(
    `SELECT u.id, u.nickname,
            (SELECT COUNT(*) FROM direct_messages m
              WHERE m.receiver_id = f.user_id AND m.sender_id = u.id AND m.read_at IS NULL) AS unread
       FROM friends f JOIN users u ON u.id = f.friend_id
      WHERE f.user_id = ?
      ORDER BY u.nickname`,
    [userId],
  );
  return rows.map(r => ({ id: r.id, nickname: r.nickname, unread: Number(r.unread) }));
}

async function friendIds(userId) {
  const [rows] = await pool.query('SELECT friend_id FROM friends WHERE user_id = ?', [userId]);
  return rows.map(r => r.friend_id);
}

async function areFriends(userId, otherId) {
  const [rows] = await pool.query('SELECT 1 FROM friends WHERE user_id = ? AND friend_id = ?', [userId, otherId]);
  return rows.length > 0;
}

// 닉네임으로 유저 찾기 { id, nickname }, 없으면 null
//   예전에 만들어진 계정끼리 닉네임이 겹쳐 누구인지 알 수 없으면 'ambiguous'
async function findByNickname(nickname) {
  const [rows] = await pool.query('SELECT id, nickname FROM users WHERE nickname = ? LIMIT 2', [nickname]);
  if (rows.length > 1) return 'ambiguous';
  return rows[0] ?? null;
}

// 친구 추가 -> 'ok' | 'already' (이미 친구) | 'full' (둘 중 한 명의 친구가 MAX_FRIENDS 명)
async function add(userId, friendId) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const [counts] = await conn.query(
      `SELECT user_id, COUNT(*) AS n, SUM(friend_id = ?) AS already FROM friends
        WHERE user_id IN (?, ?) GROUP BY user_id`,
      [friendId, userId, friendId],
    );
    let result = 'ok';
    if (counts.some(c => c.user_id === userId && Number(c.already) > 0)) result = 'already';
    else if (counts.some(c => c.n >= MAX_FRIENDS)) result = 'full';
    else {
      // 둘이 동시에 서로를 추가해도 오류가 나지 않게 IGNORE
      await conn.query(
        'INSERT IGNORE INTO friends (user_id, friend_id) VALUES (?, ?), (?, ?)',
        [userId, friendId, friendId, userId],
      );
    }
    await conn.commit();
    return result;
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

// 친구 삭제 (서로의 목록에서 지워짐), 친구가 아니었으면 false. 주고받은 1:1 채팅은 남겨 둔다
async function remove(userId, friendId) {
  const [result] = await pool.query(
    'DELETE FROM friends WHERE (user_id = ? AND friend_id = ?) OR (user_id = ? AND friend_id = ?)',
    [userId, friendId, friendId, userId],
  );
  return result.affectedRows > 0;
}

// 1:1 채팅 저장 -> { id, sender, text }
async function addMessage(senderId, receiverId, text) {
  const [result] = await pool.query(
    'INSERT INTO direct_messages (sender_id, receiver_id, text) VALUES (?, ?, ?)',
    [senderId, receiverId, text],
  );
  return { id: result.insertId, sender: senderId, text };
}

// 친구와 주고받은 1:1 채팅 [{ id, sender, text }] (오래된 것부터), 받은 메시지는 읽은 것으로 표시
//   afterId 보다 뒤의 메시지 중 최근 DM_PAGE_SIZE 개 (처음 열 때는 afterId = 0)
async function messages(userId, friendId, afterId) {
  const [rows] = await pool.query(
    `SELECT id, sender_id AS sender, text FROM direct_messages
      WHERE ((sender_id = ? AND receiver_id = ?) OR (sender_id = ? AND receiver_id = ?)) AND id > ?
      ORDER BY id DESC LIMIT ?`,
    [userId, friendId, friendId, userId, afterId, DM_PAGE_SIZE],
  );
  await pool.query(
    'UPDATE direct_messages SET read_at = UTC_TIMESTAMP(3) WHERE receiver_id = ? AND sender_id = ? AND read_at IS NULL',
    [userId, friendId],
  );
  return rows.reverse();
}

module.exports = { list, friendIds, areFriends, findByNickname, add, remove, addMessage, messages };
