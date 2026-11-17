const pool = require('./db');
const catalog = require('./catalog');
const { STARTING_RESOURCES, MAX_RESOURCE } = require('./config');

// 유저의 재화 DB 처리. 서버의 값이 기준이고, 바뀔 때마다 채팅 서버가 그 유저에게 알려 준다 (chatServer.js)
//   gold 는 users.gold 에, 그 밖의 자원(wood, stone, ...)과 장식 아이템은 inventory 에 item_id 별로 저장한다
//   장식 아이템의 item_id 는 장식물 건물 종류(buildingID)와 같다

// 항상 알려 주는 자원과 장식 아이템 (가진 적이 없어도 0 으로)
const ITEMS = [...new Set([...Object.keys(STARTING_RESOURCES), ...catalog.items, ...catalog.decorations])];

// 지금 가진 재화 { wood, stone, gold, ... }, 없는 유저면 null
async function get(userId, conn = pool) {
  const [users] = await conn.query('SELECT gold FROM users WHERE id = ?', [userId]);
  if (!users.length) return null;
  const [rows] = await conn.query('SELECT item_id, count FROM inventory WHERE user_id = ?', [userId]);

  const out = {};
  for (const item of ITEMS) out[item] = 0;
  for (const row of rows) out[row.item_id] = row.count;
  out.gold = users[0].gold;
  return out;
}

// 재화를 바꿈. deltas: { 자원: 변화량 } (양수는 획득, 음수는 사용). 하나라도 모자라면 false
//   반드시 트랜잭션 안(conn)에서 부르고, false 면 부른 쪽이 rollback 해서 먼저 바뀐 자원까지 되돌린다
async function change(conn, userId, deltas) {
  for (const [item, delta] of Object.entries(deltas)) {
    if (!Number.isInteger(delta) || delta === 0) continue;

    if (delta > 0) {
      if (item === 'gold') {
        await conn.query('UPDATE users SET gold = LEAST(gold + ?, ?) WHERE id = ?', [delta, MAX_RESOURCE, userId]);
      } else {
        await conn.query(
          `INSERT INTO inventory (user_id, item_id, count) VALUES (?, ?, LEAST(?, ?))
           ON DUPLICATE KEY UPDATE count = LEAST(count + ?, ?)`,
          [userId, item, delta, MAX_RESOURCE, delta, MAX_RESOURCE],
        );
      }
      continue;
    }

    // 사용: 가진 만큼만 (모자라면 한 줄도 바뀌지 않는다)
    const [result] = item === 'gold'
      ? await conn.query('UPDATE users SET gold = gold - ? WHERE id = ? AND gold >= ?', [-delta, userId, -delta])
      : await conn.query(
        'UPDATE inventory SET count = count - ? WHERE user_id = ? AND item_id = ? AND count >= ?',
        [-delta, userId, item, -delta],
      );
    if (result.affectedRows !== 1) return false;
  }
  return true;
}

// 재화를 더함 (획득만, 한 번에 반영)
async function add(userId, gains) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    await change(conn, userId, gains);
    await conn.commit();
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

// 장식 아이템 1개를 버림. 장식물이 아니거나 가진 것이 없으면 false
async function discard(userId, item) {
  if (!catalog.decorations.includes(item)) return false;

  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const done = await change(conn, userId, { [item]: -1 });
    if (done) await conn.commit();
    else await conn.rollback();
    return done;
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

// 가입할 때 주는 재화 (계정을 만드는 트랜잭션 안에서 호출)
const grantStarter = (conn, userId) => change(conn, userId, STARTING_RESOURCES);

module.exports = { get, change, add, discard, grantStarter };
