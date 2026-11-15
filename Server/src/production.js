const pool = require('./db');
const catalog = require('./catalog');
const resources = require('./resources');

// 건물의 주기적 자원 생산 (예: Blacksmith 는 10초마다 Stone +10)
//   건물마다 produced_at(생산을 여기까지 정산했다는 시각)을 DB 에 두고,
//   정산할 때 "지난 시간 / 생산 주기" 번 생산한 것으로 계산해 주인의 재화에 더한다
//   접속해 있지 않아도, 서버가 꺼져 있었어도 시간은 흐르므로 다음 정산 때 그동안의 생산이 한꺼번에 들어온다
//   접속 중인 유저는 채팅 서버가 생산 시각마다 정산해서 바로 알려 준다 (chatServer.js)

// 이 유저가 지은 모든 건물(멀티버스, 내 마을)의 생산을 now 까지 정산
//   -> { gained: 이번에 생산된 자원 { 자원: 수량 },
//        nextAt: 다음 생산 시각(ms), 생산하는 건물이 없으면 null,
//        since: 이번 정산에 포함된 가장 오래된 시각(ms), 생산된 것이 없으면 null }
async function settle(userId, now = Date.now()) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const [rows] = await conn.query(
      'SELECT building_uid, building_id AS type, produced_at FROM buildings WHERE owner_id = ? FOR UPDATE',
      [userId],
    );

    const gained = {};
    let nextAt = null;
    let since = null;
    for (const row of rows) {
      if (!catalog.produces(row.type)) continue;
      const data = catalog.get(row.type);
      const interval = Math.round(data.interval * 1000);
      const last = row.produced_at.getTime();
      const cycles = Math.floor((now - last) / interval);

      let settledAt = last;
      if (cycles > 0) {
        for (const [item, amount] of Object.entries(data.production)) {
          gained[item] = (gained[item] || 0) + amount * cycles;
        }
        settledAt = last + cycles * interval; // 주기에 못 미친 나머지 시간은 다음 정산으로 넘어간다
        since = since === null ? last : Math.min(since, last);
        await conn.query('UPDATE buildings SET produced_at = ? WHERE building_uid = ?', [new Date(settledAt), row.building_uid]);
      }
      nextAt = nextAt === null ? settledAt + interval : Math.min(nextAt, settledAt + interval);
    }

    if (Object.keys(gained).length) await resources.change(conn, userId, gained);
    await conn.commit();
    return { gained, nextAt, since };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

module.exports = { settle };
