const pool = require('./db');
const catalog = require('./catalog');
const resources = require('./resources');
const { MAX_BUILDINGS } = require('./config');

// 월드(멀티버스, 개인 마을)에 지어진 건물 DB 처리. 서버를 다시 켜도 남아 있다
//   건물: { id, type, x, y, owner }
//     id: 네트워크 id (지은 클라이언트가 만듦), type: 건물 종류(BuildingData.buildingID),
//     x, y: 기준 칸, owner: 지은 사람 닉네임
//   DB 의 village_id 가 월드 id (0 은 멀티버스, 그 외에는 그 유저의 마을), owner_id 는 지은 사람
//   건설 비용, 철거 환급도 같은 트랜잭션에서 지은 사람의 재화에 반영한다 (resources.js)

const size = type => catalog.get(type) ?? { width: 1, height: 1 };

// 두 건물이 차지하는 칸이 겹치는지 (기준 칸에서 오른쪽, 위쪽으로 width x height 칸)
function overlaps(a, b) {
  const sa = size(a.type);
  const sb = size(b.type);
  return a.x < b.x + sb.width && b.x < a.x + sa.width && a.y < b.y + sb.height && b.y < a.y + sa.height;
}

// 월드에 있는 건물 목록 (지은 순서). 서버 건물 데이터에 없는 종류는 뺀다
async function list(world) {
  const [rows] = await pool.query(
    `SELECT b.uid AS id, b.building_id AS type, b.x, b.y, u.nickname AS owner
       FROM buildings b JOIN users u ON u.id = b.owner_id
      WHERE b.village_id = ?
      ORDER BY b.building_uid`,
    [world],
  );
  return rows.filter(row => catalog.has(row.type));
}

// 건설: 비용을 내고 건물을 저장한다
//   -> 'ok' | 'unknown'(없는 건물 종류) | 'full'(건물이 너무 많음) | 'blocked'(다른 건물과 겹침)
//      | 'poor'(자원 부족) | 'duplicate'(이미 있는 id)
async function build(world, user, building, now = Date.now()) {
  const data = catalog.get(building.type);
  if (!data) return 'unknown';

  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const [rows] = await conn.query('SELECT building_id AS type, x, y FROM buildings WHERE village_id = ?', [world]);

    let result = 'ok';
    if (rows.length >= MAX_BUILDINGS) result = 'full';
    else if (rows.some(row => overlaps(row, building))) result = 'blocked';
    else if (!await resources.change(conn, user.id, negate(data.cost))) result = 'poor';
    if (result !== 'ok') {
      await conn.rollback();
      return result;
    }

    // produced_at: 생산을 여기까지 정산했다는 시각 (production.js). 지은 순간부터 생산 주기를 센다
    await conn.query(
      'INSERT INTO buildings (uid, village_id, owner_id, building_id, x, y, produced_at) VALUES (?, ?, ?, ?, ?, ?, ?)',
      [building.id, world, user.id, building.type, building.x, building.y, new Date(now)],
    );
    await conn.commit();
    return 'ok';
  } catch (err) {
    await conn.rollback();
    if (err.code === 'ER_DUP_ENTRY') return 'duplicate';
    throw err;
  } finally {
    conn.release();
  }
}

// 철거: 건물을 지우고 건설 비용 일부를 돌려준다. 이 월드에 없는 건물이거나 내가 지은 건물이 아니면 false
async function demolish(world, user, id) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const [rows] = await conn.query(
      'SELECT building_id AS type, owner_id FROM buildings WHERE uid = ? AND village_id = ? FOR UPDATE',
      [id, world],
    );
    if (!rows.length || rows[0].owner_id !== user.id) {
      await conn.rollback();
      return false;
    }
    await conn.query('DELETE FROM buildings WHERE uid = ?', [id]);
    await resources.change(conn, user.id, catalog.refund(rows[0].type));
    await conn.commit();
    return true;
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

// 이동: 내가 지은 건물을 다른 건물과 겹치지 않는 칸으로 옮긴다. 옮겼으면 { id, type, x, y }, 아니면 null
async function move(world, user, id, x, y) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const [rows] = await conn.query(
      'SELECT uid AS id, building_id AS type, x, y, owner_id FROM buildings WHERE village_id = ? FOR UPDATE',
      [world],
    );
    const mine = rows.find(row => row.id === id);
    const moved = mine && { id, type: mine.type, x, y };
    if (!mine || mine.owner_id !== user.id || rows.some(row => row !== mine && overlaps(row, moved))) {
      await conn.rollback();
      return null;
    }
    await conn.query('UPDATE buildings SET x = ?, y = ? WHERE uid = ?', [x, y, id]);
    await conn.commit();
    return moved;
  } catch (err) {
    await conn.rollback();
    if (err.code === 'ER_DUP_ENTRY') return null;
    throw err;
  } finally {
    conn.release();
  }
}

const negate = amounts => Object.fromEntries(Object.entries(amounts).map(([item, n]) => [item, -n]));

module.exports = { list, build, demolish, move, overlaps };
