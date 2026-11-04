const { MAX_BUILDINGS } = require('./config');

// 맵에 지어진 건물 (서버 메모리에만 보관, 서버를 다시 켜면 비워짐)
//   id -> { id, type, x, y, owner }   type 은 건물 종류(BuildingData.buildingID), x, y 는 기준 칸
class Buildings {
  constructor() {
    this.byId = new Map();
  }

  // 건설, 이미 있는 id 이거나 건물이 너무 많으면 false
  add(building) {
    if (this.byId.has(building.id) || this.byId.size >= MAX_BUILDINGS) return false;
    this.byId.set(building.id, building);
    return true;
  }

  // 철거, 없는 건물이면 false
  remove(id) {
    return this.byId.delete(id);
  }

  // 이동, 없는 건물이면 null
  move(id, x, y) {
    const building = this.byId.get(id);
    if (!building) return null;
    building.x = x;
    building.y = y;
    return building;
  }

  list() {
    return [...this.byId.values()];
  }
}

module.exports = Buildings;
