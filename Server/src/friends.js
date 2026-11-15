const pool = require('./db');
const { MAX_FRIENDS, MAX_FRIEND_REQUESTS, DM_PAGE_SIZE } = require('./config');

// 친구, 친구 요청, 1:1 채팅 DB 처리
//   친구 요청: friend_requests 에 보낸 사람 -> 받은 사람 한 줄. 받은 사람이 수락해야 친구가 된다
//             (거절하거나 보낸 사람이 취소하면 요청만 지워진다)
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

// 아직 수락, 거절하지 않은 친구 요청 (오래된 것부터)
//   -> { incoming: [{ id, nickname }] 나에게 요청을 보낸 사람들, outgoing: [{ id, nickname }] 내가 요청을 보내 둔 사람들 }
async function requests(userId) {
  const [incoming] = await pool.query(
    `SELECT u.id, u.nickname FROM friend_requests r JOIN users u ON u.id = r.sender_id
      WHERE r.receiver_id = ? ORDER BY r.created_at, u.id`,
    [userId],
  );
  const [outgoing] = await pool.query(
    `SELECT u.id, u.nickname FROM friend_requests r JOIN users u ON u.id = r.receiver_id
      WHERE r.sender_id = ? ORDER BY r.created_at, u.id`,
    [userId],
  );
  const toUser = r => ({ id: r.id, nickname: r.nickname });
  return { incoming: incoming.map(toUser), outgoing: outgoing.map(toUser) };
}

async function friendIds(userId) {
  const [rows] = await pool.query('SELECT friend_id FROM friends WHERE user_id = ?', [userId]);
  return rows.map(r => r.friend_id);
}

// 친구와, 친구 요청을 주고받은 상대의 유저 id (탈퇴할 때 이 사람들의 화면에서도 사라지도록 알리는 데 쓴다)
async function contactIds(userId) {
  const [rows] = await pool.query(
    `SELECT friend_id AS id FROM friends WHERE user_id = ?
      UNION SELECT receiver_id FROM friend_requests WHERE sender_id = ?
      UNION SELECT sender_id FROM friend_requests WHERE receiver_id = ?`,
    [userId, userId, userId],
  );
  return rows.map(r => r.id);
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

// 두 유저 사이의 친구 관계를 바꾸는 작업을 트랜잭션으로 실행
//   두 유저의 줄을 id 순서로 잠그므로, 둘이 동시에 서로에게 요청하거나 수락과 취소가 겹쳐도 한 번에 하나씩 처리된다
//   그 사이 한 명이 탈퇴했으면 work 를 실행하지 않고 'gone'
async function withPair(a, b, work) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const [users] = await conn.query('SELECT id FROM users WHERE id IN (?, ?) ORDER BY id FOR UPDATE', [a, b]);
    const result = users.length === 2 ? await work(conn) : 'gone';
    await conn.commit();
    return result;
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

// 두 유저의 지금 관계와 한도 확인에 필요한 수 (me 기준)
async function relation(conn, me, other) {
  const [[row]] = await conn.query(
    `SELECT
       (SELECT COUNT(*) FROM friends WHERE user_id = ? AND friend_id = ?) AS friends,
       (SELECT COUNT(*) FROM friend_requests WHERE sender_id = ? AND receiver_id = ?) AS sent,
       (SELECT COUNT(*) FROM friend_requests WHERE sender_id = ? AND receiver_id = ?) AS received,
       (SELECT COUNT(*) FROM friends WHERE user_id = ?) AS myFriends,
       (SELECT COUNT(*) FROM friends WHERE user_id = ?) AS theirFriends,
       (SELECT COUNT(*) FROM friend_requests WHERE sender_id = ?) AS myRequests,
       (SELECT COUNT(*) FROM friend_requests WHERE receiver_id = ?) AS theirInbox`,
    [me, other, me, other, other, me, me, other, me, other],
  );
  return {
    friends: Number(row.friends) > 0,   // 이미 친구
    sent: Number(row.sent) > 0,         // 내가 보낸 요청이 있음
    received: Number(row.received) > 0, // 상대가 보낸 요청이 있음
    myFriends: Number(row.myFriends),
    theirFriends: Number(row.theirFriends),
    myRequests: Number(row.myRequests), // 내가 보내 둔 요청 수
    theirInbox: Number(row.theirInbox), // 상대가 받아 둔 요청 수
  };
}

// 서로 친구로 만들고 둘 사이의 요청은 지운다
async function makeFriends(conn, a, b) {
  await conn.query('INSERT IGNORE INTO friends (user_id, friend_id) VALUES (?, ?), (?, ?)', [a, b, b, a]);
  await conn.query(
    'DELETE FROM friend_requests WHERE (sender_id = ? AND receiver_id = ?) OR (sender_id = ? AND receiver_id = ?)',
    [a, b, b, a],
  );
}

// 친구 요청 보내기
//   -> 'requested' 요청을 보냄 (상대가 수락하면 친구가 됨)
//      'accepted'  상대가 먼저 보낸 요청이 있어서 바로 친구가 됨
//      'already'   이미 친구
//      'pending'   이미 요청을 보내 둠
//      'full'      내 친구가 MAX_FRIENDS 명 (바로 친구가 되는 경우에는 상대의 친구 수도 확인)
//      'too_many'  내가 보내 둔 요청이나 상대가 받아 둔 요청이 MAX_FRIEND_REQUESTS 개
//      'gone'      그 사이 상대가 탈퇴함
function request(userId, targetId) {
  return withPair(userId, targetId, async conn => {
    const r = await relation(conn, userId, targetId);
    if (r.friends) return 'already';
    if (r.sent) return 'pending';
    if (r.myFriends >= MAX_FRIENDS) return 'full';

    if (r.received) {
      if (r.theirFriends >= MAX_FRIENDS) return 'full';
      await makeFriends(conn, userId, targetId);
      return 'accepted';
    }

    if (r.myRequests >= MAX_FRIEND_REQUESTS || r.theirInbox >= MAX_FRIEND_REQUESTS) return 'too_many';
    await conn.query('INSERT INTO friend_requests (sender_id, receiver_id) VALUES (?, ?)', [userId, targetId]);
    return 'requested';
  });
}

// 받은 친구 요청 수락 (senderId 가 나에게 보낸 요청)
//   -> 'ok' 친구가 됨 | 'none' 그런 요청이 없음 (상대가 취소했음) | 'full' 둘 중 한 명의 친구가 MAX_FRIENDS 명 | 'gone' 상대가 탈퇴함
function accept(userId, senderId) {
  return withPair(userId, senderId, async conn => {
    const r = await relation(conn, userId, senderId);
    if (!r.received) return 'none';
    if (r.myFriends >= MAX_FRIENDS || r.theirFriends >= MAX_FRIENDS) return 'full';
    await makeFriends(conn, userId, senderId);
    return 'ok';
  });
}

// 친구 요청 지우기: 받은 사람이 거절하거나 보낸 사람이 취소. 그런 요청이 없었으면 false
async function removeRequest(senderId, receiverId) {
  const [result] = await pool.query(
    'DELETE FROM friend_requests WHERE sender_id = ? AND receiver_id = ?',
    [senderId, receiverId],
  );
  return result.affectedRows > 0;
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

module.exports = {
  list, requests, friendIds, contactIds, areFriends, findByNickname,
  request, accept, removeRequest, remove, addMessage, messages,
};
