const pool = require('./db');
const catalog = require('./catalog');
const resources = require('./resources');
const { HARVEST_GRACE_MS, MULTIVERSE } = require('./config');

// 건물 수확 (클라이언트 BuildingHarvest: 건물 위에 뜨는 수확 버튼)
//   장식이 아닌 건물은 지은 뒤 수확 주기(harvestSeconds)가 지나면 수확할 수 있다
//   수확하면 그 건물의 수확량(harvest)을 주인의 재화에 더하고 주기를 처음부터 다시 센다
//   수확하지 않고 두어도 더 쌓이지는 않는다 (접속하지 않은 동안에도 한 번 수확할 양만 기다린다)
//   건물마다 harvested_at(마지막으로 수확한 시각, 수확한 적이 없으면 지은 시각)을 DB 에 둔다
//   내가 지은 건물만 수확할 수 있다 (친구 마을에는 내 건물이 없다)
//   멀티버스는 모두가 같이 쓰는 공용 공간이라 그곳에 있는 건물은 수확하지 않는다

// 수확까지 남은 시간(초, 올림). 0 이면 지금 수확할 수 있다
const remaining = (data, harvestedAt, now) =>
  Math.max(0, Math.ceil((harvestedAt.getTime() + data.harvestSeconds * 1000 - now) / 1000));

// 이 월드에 있는 내 건물 중 수확하는 건물의 타이머 [{ id, remaining }]
async function timers(world, userId, now = Date.now()) {
  if (world === MULTIVERSE) return [];
  const [rows] = await pool.query(
    'SELECT uid AS id, building_id AS type, harvested_at FROM buildings WHERE village_id = ? AND owner_id = ? ORDER BY building_uid',
    [world, userId],
  );
  return rows
    .filter(row => catalog.harvestable(row.type))
    .map(row => ({ id: row.id, remaining: remaining(catalog.get(row.type), row.harvested_at, now) }));
}

// 수확: 이 월드에 있는 내 건물 id 의 수확량을 재화에 더한다
//   -> { result: 'ok', gained: { 자원: 수량 }, type, timer }  수확함. timer: 다음 수확까지의 타이머 { id, remaining }
//      { result: 'early', timer }                              아직 수확할 수 없음. timer: 남은 시간
//      { result: 'none' }                                      이 월드에 없는 건물, 남의 건물, 수확하지 않는 건물
async function harvest(world, userId, id, now = Date.now()) {
  if (world === MULTIVERSE) return { result: 'none' };
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const [rows] = await conn.query(
      'SELECT building_id AS type, owner_id, harvested_at FROM buildings WHERE uid = ? AND village_id = ? FOR UPDATE',
      [id, world],
    );
    const row = rows[0];
    if (!row || row.owner_id !== userId || !catalog.harvestable(row.type)) {
      await conn.rollback();
      return { result: 'none' };
    }

    const data = catalog.get(row.type);
    const readyAt = row.harvested_at.getTime() + data.harvestSeconds * 1000;
    if (now < readyAt - HARVEST_GRACE_MS) {
      await conn.rollback();
      return { result: 'early', timer: { id, remaining: remaining(data, row.harvested_at, now) } };
    }

    await resources.change(conn, userId, data.harvest);
    await conn.query('UPDATE buildings SET harvested_at = ? WHERE uid = ?', [new Date(now), id]);
    await conn.commit();
    return { result: 'ok', gained: data.harvest, type: row.type, timer: { id, remaining: Math.ceil(data.harvestSeconds) } };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

module.exports = { timers, harvest };
