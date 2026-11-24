const pool = require('./db');
const catalog = require('./catalog');
const resources = require('./resources');

// 대장간 제작: 자원을 쓰고 도구(예: 삽)를 인벤토리에 넣는다
//   제작법은 data/crafting.json (catalog.recipe). 도구는 inventory 에 그 이름(item_id)으로 들어간다
//   max 개까지만 가질 수 있다 (삽은 1개)

const SHOVEL = 'shovel';

// 만들기 -> 'ok' | 'unknown'(없는 제작법) | 'have'(이미 최대로 가짐) | 'poor'(자원 부족)
async function craft(userId, item) {
  const recipe = catalog.recipe(item);
  if (!recipe) return 'unknown';

  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    // FOR UPDATE: 같은 사람이 동시에 두 번 만들어 최대 개수를 넘지 않도록 잠근다
    const [rows] = await conn.query(
      'SELECT count FROM inventory WHERE user_id = ? AND item_id = ? FOR UPDATE', [userId, item],
    );
    const have = rows.length ? rows[0].count : 0;

    let result = 'ok';
    if (have >= recipe.max) result = 'have';
    else {
      const deltas = { [item]: 1 };
      for (const [res, n] of Object.entries(recipe.cost)) deltas[res] = -n;
      if (!await resources.change(conn, userId, deltas)) result = 'poor';
    }

    if (result === 'ok') await conn.commit();
    else await conn.rollback();
    return result;
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

// 이 도구를 가지고 있는지
async function has(userId, item) {
  const [rows] = await pool.query('SELECT count FROM inventory WHERE user_id = ? AND item_id = ?', [userId, item]);
  return rows.length > 0 && rows[0].count > 0;
}

module.exports = { craft, has, SHOVEL };
