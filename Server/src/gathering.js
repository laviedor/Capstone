const pool = require('./db');
const catalog = require('./catalog');
const resources = require('./resources');
const { GATHER_SPACING_RATE, EXPEDITION_GRACE_MS } = require('./config');

// 채집: 필드의 자원 노드, 채집 타이머. 보상은 서버가 정해서 재화에 더한다 (catalog.js 의 데이터)

// ---------- 필드의 자원 노드 (나무, 돌, 금: 캐면 자원을 주고 일정 시간 뒤 다시 생김) ----------
//   노드가 어디에 있는지는 서버가 모르므로 횟수만 제한한다:
//   유저마다 "그 종류 노드 수(count)만큼, 재생성 시간(respawnSeconds)마다" 캘 수 있고,
//   한 번 캐는 데 걸리는 시간(gatherSeconds)보다 빨리 연달아 캘 수 없다
const recentGathers = new Map(); // '유저 id|노드 종류' -> 최근에 캔 시각들(ms)

// 노드를 캠 -> { item, amount } | 'unknown'(없는 노드 종류) | 'too_fast'(너무 자주 캠)
async function gatherNode(userId, nodeId, now = Date.now()) {
  const node = catalog.node(nodeId);
  if (!node) return 'unknown';

  const key = `${userId}|${nodeId}`;
  const times = (recentGathers.get(key) ?? []).filter(t => now - t < node.respawnSeconds * 1000);
  const last = times[times.length - 1];
  const tooSoon = last !== undefined && now - last < node.gatherSeconds * 1000 * GATHER_SPACING_RATE;
  if (times.length >= node.count || tooSoon) {
    recentGathers.set(key, times);
    return 'too_fast';
  }
  times.push(now);
  recentGathers.set(key, times);

  await resources.add(userId, { [node.item]: node.amount });
  return { item: node.item, amount: node.amount };
}

// ---------- 채집 타이머 (장소를 골라 시작하면 정해진 시간 뒤에 보상을 받는다) ----------
//   expeditions 테이블에 끝나는 시각을 저장하므로 게임을 꺼도, 서버를 다시 켜도 계속 진행된다
//   보상을 받기 전(claimed_at 이 NULL)인 것은 유저당 하나만 둔다

// 진행 중이거나 보상을 받지 않은 채집 타이머 { id, area, endAt(ms) }, 없으면 null
async function currentExpedition(userId, conn = pool, lock = false) {
  const [rows] = await conn.query(
    `SELECT id, area_id AS area, end_at FROM expeditions
      WHERE user_id = ? AND claimed_at IS NULL ORDER BY id DESC LIMIT 1${lock ? ' FOR UPDATE' : ''}`,
    [userId],
  );
  return rows.length ? { id: rows[0].id, area: rows[0].area, endAt: rows[0].end_at.getTime() } : null;
}

// 시작 -> { started: { area, endAt } }
//         | { existing: { area, endAt } }   이미 진행 중이거나 보상을 받지 않은 것이 있음
//         | 'unknown'                       없는 장소
async function startExpedition(userId, area, now = Date.now()) {
  const data = catalog.expedition(area);
  if (!data) return 'unknown';

  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    // 유저 줄을 잠가 같은 유저의 요청이 동시에 두 개 시작하지 못하게 한다
    await conn.query('SELECT id FROM users WHERE id = ? FOR UPDATE', [userId]);
    const existing = await currentExpedition(userId, conn);
    if (existing) {
      await conn.rollback();
      return { existing };
    }
    const endAt = now + Math.round(data.seconds * 1000);
    await conn.query(
      'INSERT INTO expeditions (user_id, area_id, started_at, end_at) VALUES (?, ?, ?, ?)',
      [userId, area, new Date(now), new Date(endAt)],
    );
    await conn.commit();
    return { started: { area, endAt } };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

// 보상 수령 -> { reward: { area, gold, decoration } }   decoration: 받은 장식물 종류 (못 받았으면 null)
//             | { pending: { area, endAt } }            아직 끝나지 않음
//             | null                                    받을 것이 없음
async function claimExpedition(userId, now = Date.now()) {
  const conn = await pool.getConnection();
  try {
    await conn.beginTransaction();
    const expedition = await currentExpedition(userId, conn, true);
    if (!expedition) {
      await conn.rollback();
      return null;
    }
    if (now < expedition.endAt - EXPEDITION_GRACE_MS) {
      await conn.rollback();
      return { pending: { area: expedition.area, endAt: expedition.endAt } };
    }

    // 데이터에서 빠진 장소면 보상 없이 끝낸다
    const data = catalog.expedition(expedition.area) ?? { gold: 0, decorationChance: 0 };
    const candidates = catalog.decorationRewards;
    const decoration = candidates.length > 0 && Math.random() < data.decorationChance
      ? candidates[Math.floor(Math.random() * candidates.length)]
      : null;
    const gained = {};
    if (data.gold > 0) gained.gold = data.gold;
    if (decoration) gained[decoration] = 1;

    await resources.change(conn, userId, gained);
    await conn.query('UPDATE expeditions SET claimed_at = ? WHERE id = ?', [new Date(now), expedition.id]);
    await conn.commit();
    return { reward: { area: expedition.area, gold: data.gold, decoration } };
  } catch (err) {
    await conn.rollback();
    throw err;
  } finally {
    conn.release();
  }
}

module.exports = { gatherNode, currentExpedition, startExpedition, claimExpedition };
