using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class PlacedTile {
    public Vector3Int pos;
    public TileBase tile;
    public int layer;

    public PlacedTile(Vector3Int pos, TileBase tile, int layer) {
        this.pos = pos;
        this.tile = tile;
        this.layer = layer;
    }
}
