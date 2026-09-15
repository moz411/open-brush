// Copyright 2026
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush {
  /// <summary>
  /// Experimental voxel brush for Open Brush.
  ///
  /// Pointer positions are snapped to a regular 3D grid.  The stroke is kept as
  /// one Mesh; no GameObject is created per voxel. Faces shared by adjacent
  /// voxels are removed when rebuilding the mesh.
  ///
  /// Prototype note: this brush deliberately does not support Open Brush batch
  /// geometry yet. It is intended first for validating interaction/performance
  /// on Quest before wiring it into the brush catalogue and serialization.
  /// </summary>
  [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
  public class VoxelBrushScript : BaseBrushScript {
    private readonly HashSet<Vector3Int> m_Voxels = new HashSet<Vector3Int>();
    private Mesh m_Mesh;
    private bool m_Dirty;
    private float m_CellSize;

    // Conservative prototype limit: exposed here so Quest testing cannot
    // accidentally generate an unbounded mesh in a single stroke.
    private const int kMaxVoxels = 8192;

    private static readonly Vector3Int[] kNeighbors = {
      Vector3Int.right, Vector3Int.left,
      Vector3Int.up, Vector3Int.down,
      new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    // Four corners for each outward-facing cube face, counter-clockwise as seen
    // from outside. Coordinates are relative to the voxel centre, in half-cells.
    private static readonly Vector3[,] kFaceCorners = {
      { new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(.5f,.5f,-.5f) },
      { new Vector3(-.5f,-.5f,.5f), new Vector3(-.5f,-.5f,-.5f), new Vector3(-.5f,.5f,-.5f), new Vector3(-.5f,.5f,.5f) },
      { new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) },
      { new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(-.5f,-.5f,-.5f) },
      { new Vector3(.5f,-.5f,.5f), new Vector3(-.5f,-.5f,.5f), new Vector3(-.5f,.5f,.5f), new Vector3(.5f,.5f,.5f) },
      { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f) }
    };

    protected VoxelBrushScript() : base(bCanBatch: false) { }

    protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
      base.InitBrush(desc, localPointerXf);
      // Brush size becomes grid pitch. Clamp protects against zero/near-zero
      // cells while preserving normal Open Brush pointer scaling.
      m_CellSize = Mathf.Max(BaseSize_LS, 0.001f);
      m_Mesh = new Mesh { name = "Voxel Brush Stroke" };
      m_Mesh.indexFormat = IndexFormat.UInt32;
      GetComponent<MeshFilter>().sharedMesh = m_Mesh;
    }

    public override GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
      var layout = new GeometryPool.VertexLayout();
      layout.bUseColors = true;
      layout.bUseNormals = true;
      layout.bUseTangents = false;
      layout.bUseVertexIds = false;
      return layout;
    }

    protected override bool UpdatePositionImpl(Vector3 vPos, Quaternion ori, float pressure) {
      if (m_Voxels.Count >= kMaxVoxels) { return false; }

      Vector3Int cell = WorldToCell(vPos);
      if (!m_Voxels.Add(cell)) { return false; }

      m_Dirty = true;
      return true;
    }

    private Vector3Int WorldToCell(Vector3 p) {
      return new Vector3Int(
        Mathf.RoundToInt(p.x / m_CellSize),
        Mathf.RoundToInt(p.y / m_CellSize),
        Mathf.RoundToInt(p.z / m_CellSize));
    }

    public override void ApplyChangesToVisuals() {
      if (!m_Dirty || m_Mesh == null) { return; }
      RebuildMesh();
      m_Dirty = false;
    }

    private void RebuildMesh() {
      var vertices = new List<Vector3>(m_Voxels.Count * 12);
      var normals = new List<Vector3>(m_Voxels.Count * 12);
      var colors = new List<Color32>(m_Voxels.Count * 12);
      var triangles = new List<int>(m_Voxels.Count * 18);

      foreach (Vector3Int cell in m_Voxels) {
        Vector3 center = (Vector3)cell * m_CellSize;

        for (int face = 0; face < 6; ++face) {
          if (m_Voxels.Contains(cell + kNeighbors[face])) { continue; }

          int first = vertices.Count;
          Vector3 normal = (Vector3)kNeighbors[face];
          for (int corner = 0; corner < 4; ++corner) {
            vertices.Add(center + kFaceCorners[face, corner] * m_CellSize);
            normals.Add(normal);
            colors.Add(m_Color);
          }

          triangles.Add(first + 0);
          triangles.Add(first + 1);
          triangles.Add(first + 2);
          triangles.Add(first + 0);
          triangles.Add(first + 2);
          triangles.Add(first + 3);
        }
      }

      m_Mesh.Clear();
      m_Mesh.SetVertices(vertices);
      m_Mesh.SetNormals(normals);
      m_Mesh.SetColors(colors);
      m_Mesh.SetTriangles(triangles, 0, true);
      m_Mesh.RecalculateBounds();
    }

    public override int GetNumUsedVerts() {
      return m_Mesh == null ? 0 : m_Mesh.vertexCount;
    }

    public override float GetSpawnInterval(float pressure01) {
      // Dense enough to avoid holes while moving the controller. Grid snapping
      // suppresses duplicate samples.
      return Mathf.Max(m_CellSize * LOCAL_TO_POINTER * 0.45f, 0.001f);
    }

    public override bool IsOutOfVerts() {
      return m_Voxels.Count >= kMaxVoxels;
    }

    public override bool ShouldDiscard() {
      return m_Voxels.Count == 0;
    }

    public override void FinalizeSolitaryBrush() {
      ApplyChangesToVisuals();
    }

    public override BatchSubset FinalizeBatchedBrush() {
      // bCanBatch=false: this should not be called for the prototype.
      return null;
    }

    protected override void InitUndoClone(GameObject clone) { }

    public override void DebugGetGeometry(
        out Vector3[] verts, out int nVerts,
        out Vector2[] uv0s, out int[] tris, out int nTris) {
      if (m_Mesh == null) {
        verts = null; nVerts = 0; uv0s = null; tris = null; nTris = 0;
        return;
      }
      verts = m_Mesh.vertices;
      nVerts = verts.Length;
      uv0s = null;
      tris = m_Mesh.triangles;
      nTris = tris.Length;
    }
  }
}
