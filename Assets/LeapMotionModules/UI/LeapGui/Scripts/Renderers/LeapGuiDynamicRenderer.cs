﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Leap.Unity.Query;
using Leap.Unity.Attributes;

[AddComponentMenu("")]
[LeapGuiTag("Dynamic")]
public class LeapGuiDynamicRenderer : LeapGuiRenderer,
  ISupportsAddRemove,
  ISupportsFeature<LeapGuiMeshFeature>,
  ISupportsFeature<LeapGuiTextureFeature> {

  [SerializeField]
  private Shader _shader;

  [MinValue(0)]
  [SerializeField]
  private int _borderAmount;

  [Header("Debug")]
  [HideInInspector, SerializeField]
  private List<Mesh> _elementMeshes = new List<Mesh>();

  [HideInInspector, SerializeField]
  private Material _material;

  //Feature lists
  private List<LeapGuiMeshFeature> _meshFeatures = new List<LeapGuiMeshFeature>();
  private List<LeapGuiTextureFeature> _textureFeatures = new List<LeapGuiTextureFeature>();

  //Textures
  private Dictionary<UVChannelFlags, Rect[]> _atlasedRects;

  //Cylindrical space
  private const string CYLINDRICAL_PARAMETERS = LeapGui.PROPERTY_PREFIX + "Cylindrical_ElementParameters";
  private List<Matrix4x4> _cylindrical_worldToAnchor = new List<Matrix4x4>();
  private List<Matrix4x4> _cylindrical_meshTransforms = new List<Matrix4x4>();
  private List<Vector4> _cylindrical_elementParameters = new List<Vector4>();

  //Spherical space
  private const string SPHERICAL_PARAMETERS = LeapGui.PROPERTY_PREFIX + "Spherical_ElementParameters";
  private List<Matrix4x4> _spherical_worldToAnchor = new List<Matrix4x4>();
  private List<Matrix4x4> _spherical_meshTransforms = new List<Matrix4x4>();
  private List<Vector4> _spherical_elementParameters = new List<Vector4>();

  public void OnAddElements(List<LeapGuiElement> elements, List<int> indexes) {
    //TODO, this is super slow and sad
    OnUpdateRendererEditor();
  }

  public void OnRemoveElements(List<int> toRemove) {
    //TODO, this is super slow and sad
    OnUpdateRendererEditor();
  }

  public void GetSupportInfo(List<LeapGuiMeshFeature> features, List<SupportInfo> info) { }

  public void GetSupportInfo(List<LeapGuiTextureFeature> features, List<SupportInfo> info) {
    SupportUtil.OnlySupportFirstFeature(features, info);
  }

  public override SupportInfo GetSpaceSupportInfo(LeapGuiSpace space) {
    if (space is LeapGuiRectSpace ||
        space is LeapGuiCylindricalSpace ||
        space is LeapGuiSphericalSpace) {
      return SupportInfo.FullSupport();
    } else {
      return SupportInfo.Error("Dynamic Renderer does not support " + space.GetType().Name);
    }
  }

  public override void OnEnableRenderer() {
  }

  public override void OnDisableRenderer() {
  }

  public override void OnUpdateRenderer() {
    if (gui.space is LeapGuiRectSpace) {
      for (int i = 0; i < gui.elements.Count; i++) {
        Graphics.DrawMesh(_elementMeshes[i], gui.elements[i].transform.localToWorldMatrix, _material, 0);
      }
    } else if (gui.space is LeapGuiCylindricalSpace) {
      var cylindricalSpace = gui.space as LeapGuiCylindricalSpace;

      if (_cylindrical_worldToAnchor.Count != gui.elements.Count) {
        _cylindrical_worldToAnchor.Fill(gui.elements.Count, Matrix4x4.identity);
      }

      if (_cylindrical_elementParameters.Count != gui.elements.Count) {
        _cylindrical_elementParameters.Fill(gui.elements.Count, Vector4.zero);
      }

      if (_cylindrical_meshTransforms.Count != gui.elements.Count) {
        _cylindrical_meshTransforms.Fill(gui.elements.Count, Matrix4x4.identity);
      }

      Profiler.BeginSample("Assemble Data");
      for (int i = 0; i < _elementMeshes.Count; i++) {
        var element = gui.elements[i];
        var parameters = cylindricalSpace.GetElementParameters(element.anchor, element.transform.position);
        var pos = cylindricalSpace.TransformPoint(element, gui.transform.InverseTransformPoint(element.transform.position));

        Quaternion rot = Quaternion.Euler(0, parameters.angleOffset * Mathf.Rad2Deg, 0);

        Matrix4x4 guiMesh = transform.localToWorldMatrix * Matrix4x4.TRS(pos, rot, Vector3.one);
        Matrix4x4 deform = transform.worldToLocalMatrix * Matrix4x4.TRS(transform.position - element.transform.position, Quaternion.identity, Vector3.one) * element.transform.localToWorldMatrix;
        Matrix4x4 total = guiMesh * deform;

        _cylindrical_elementParameters[i] = parameters;
        _cylindrical_meshTransforms[i] = total;
        _cylindrical_worldToAnchor[i] = guiMesh.inverse;
      }
      Profiler.EndSample();

      Profiler.BeginSample("Update Material");
      _material.SetFloat(LeapGuiCylindricalSpace.RADIUS_PROPERTY, cylindricalSpace.radius);
      _material.SetMatrixArray("_LeapGuiCylindrical_WorldToAnchor", _cylindrical_worldToAnchor);
      _material.SetMatrix("_LeapGui_LocalToWorld", transform.localToWorldMatrix);
      _material.SetVectorArray("_LeapGuiCylindrical_ElementParameters", _cylindrical_elementParameters);
      Profiler.EndSample();

      Profiler.BeginSample("Draw Meshes");
      for (int i = 0; i < _elementMeshes.Count; i++) {
        Graphics.DrawMesh(_elementMeshes[i], _cylindrical_meshTransforms[i], _material, 0);
      }
      Profiler.EndSample();
    } else if (gui.space is LeapGuiSphericalSpace) {
      var sphericalSpace = gui.space as LeapGuiSphericalSpace;

      if (_spherical_worldToAnchor.Count != gui.elements.Count) {
        _spherical_worldToAnchor.Fill(gui.elements.Count, Matrix4x4.identity);
      }

      if (_spherical_elementParameters.Count != gui.elements.Count) {
        _spherical_elementParameters.Fill(gui.elements.Count, Vector4.zero);
      }

      if (_spherical_meshTransforms.Count != gui.elements.Count) {
        _spherical_meshTransforms.Fill(gui.elements.Count, Matrix4x4.identity);
      }

      for (int i = 0; i < _elementMeshes.Count; i++) {
        var element = gui.elements[i];
        var parameters = sphericalSpace.GetElementParameters(element.anchor, element.transform.position);
        var pos = sphericalSpace.TransformPoint(element, gui.transform.InverseTransformPoint(element.transform.position));

        Quaternion rot = Quaternion.Euler(-parameters.angleYOffset * Mathf.Rad2Deg,
                                          parameters.angleXOffset * Mathf.Rad2Deg,
                                          0);

        Matrix4x4 guiMesh = transform.localToWorldMatrix * Matrix4x4.TRS(pos, rot, Vector3.one);
        Matrix4x4 deform = transform.worldToLocalMatrix * Matrix4x4.TRS(transform.position - element.transform.position, Quaternion.identity, Vector3.one) * element.transform.localToWorldMatrix;
        Matrix4x4 total = guiMesh * deform;

        _spherical_elementParameters[i] = parameters;
        _spherical_meshTransforms[i] = total;
        _spherical_worldToAnchor[i] = guiMesh.inverse;
      }

      _material.SetFloat(LeapGuiSphericalSpace.RADIUS_PROPERTY, sphericalSpace.radius);
      _material.SetMatrixArray("_LeapGuiSpherical_WorldToAnchor", _spherical_worldToAnchor);
      _material.SetMatrix("_LeapGui_LocalToWorld", transform.localToWorldMatrix);
      _material.SetVectorArray("_LeapGuiSpherical_ElementParameters", _spherical_elementParameters);

      for (int i = 0; i < _elementMeshes.Count; i++) {
        Graphics.DrawMesh(_elementMeshes[i], _spherical_meshTransforms[i], _material, 0);
      }
    }
  }

  public override void OnEnableRendererEditor() {
  }

  public override void OnDisableRendererEditor() {
  }

  public override void OnUpdateRendererEditor() {
    MeshCache.Clear();

    ensureObjectsAreValid();

    if (gui.GetFeatures(_meshFeatures)) {
      Profiler.BeginSample("Bake Verts");
      bakeVerts();
      Profiler.EndSample();

      Profiler.BeginSample("Bake Colors");
      bakeColors();
      Profiler.EndSample();

      Profiler.BeginSample("Bake Uv3");
      bakeUv3();
      Profiler.EndSample();
    }

    if (gui.GetFeatures(_textureFeatures)) {
      Profiler.BeginSample("Atlas Textures");
      atlasTextures();
      Profiler.EndSample();
    }

    Profiler.BeginSample("Bake Uvs");
    bakeUvs();
    Profiler.EndSample();

    if (gui.space is LeapGuiCylindricalSpace) {
      _material.EnableKeyword(LeapGuiCylindricalSpace.FEATURE_NAME);
    } else if (gui.space is LeapGuiSphericalSpace) {
      _material.EnableKeyword(LeapGuiSphericalSpace.FEATURE_NAME);
    }

    OnUpdateRenderer();
  }

  private void ensureObjectsAreValid() {
    //Destroy meshes that will no longer be used
    while (_elementMeshes.Count > gui.elements.Count) {
      DestroyImmediate(_elementMeshes.RemoveLast());
    }

    //Clear out the meshes that remain
    foreach (var mesh in _elementMeshes) {
      mesh.Clear();
    }

    //Create new meshes that need to exist
    while (_elementMeshes.Count < gui.elements.Count) {
      Mesh elementMesh = new Mesh();
      elementMesh.MarkDynamic();
      elementMesh.name = "Generated Element Mesh";
      _elementMeshes.Add(elementMesh);
    }

    if (_shader == null) {
      _shader = Shader.Find("LeapGui/Defaults/Dynamic");
    }

    if (_material != null) {
      DestroyImmediate(_material);
    }

    _material = new Material(_shader);
    _material.name = "Dynamic Gui Material";
  }

  private List<Vector3> _tempVertList = new List<Vector3>();
  private List<int> _tempTriList = new List<int>();
  private void bakeVerts() {
    for (int i = 0; i < gui.elements.Count; i++) {
      foreach (var meshFeature in _meshFeatures) {
        var elementData = meshFeature.data[i];
        var mesh = elementData.mesh;
        if (mesh == null) continue;

        var topology = MeshCache.GetTopology(elementData.mesh);

        int vertOffset = _tempVertList.Count;
        for (int j = 0; j < topology.tris.Length; j++) {
          _tempTriList.Add(topology.tris[j] + vertOffset);
        }

        _tempVertList.AddRange(topology.verts);
      }

      _elementMeshes[i].SetVertices(_tempVertList);
      _elementMeshes[i].SetTriangles(_tempTriList, 0);
      _tempVertList.Clear();
      _tempTriList.Clear();
    }
  }

  private List<Color> _tempColorList = new List<Color>();
  private void bakeColors() {
    //If no mesh feature wants colors, don't bake them!
    if (!_meshFeatures.Query().Any(f => f.color)) {
      return;
    }

    for (int i = 0; i < gui.elements.Count; i++) {
      foreach (var meshFeature in _meshFeatures) {
        var elementData = meshFeature.data[i];
        var mesh = elementData.mesh;
        if (mesh == null) continue;

        Color totalTint = elementData.color * meshFeature.tint;

        var colors = MeshCache.GetColors(mesh);
        if (colors != null) {
          colors.Query().Select(c => c * totalTint).AppendList(_tempColorList);
        } else {
          _tempColorList.Append(mesh.vertexCount, totalTint);
        }
      }

      _elementMeshes[i].SetColors(_tempColorList);
      _tempColorList.Clear();
    }

    _material.EnableKeyword(LeapGuiMeshFeature.COLORS_FEATURE);
  }

  private void atlasTextures() {
    Texture2D[] atlasTextures;
    AtlasUtil.DoAtlas(_textureFeatures, _borderAmount,
                  out atlasTextures,
                  out _atlasedRects);

    for (int i = 0; i < _textureFeatures.Count; i++) {
      _material.SetTexture(_textureFeatures[i].propertyName, atlasTextures[i]);
    }
  }

  private List<Vector4> _tempUvList = new List<Vector4>();
  private void bakeUvs() {
    var enabledChannels = new List<UVChannelFlags>();

    //Build up uv dictionary
    foreach (var feature in _meshFeatures) {
      foreach (var enabledChannel in feature.enabledUvChannels) {
        if (!enabledChannels.Contains(enabledChannel)) {
          enabledChannels.Add(enabledChannel);
          _material.EnableKeyword(LeapGuiMeshFeature.GetUvFeature(enabledChannel));
        }
      }
    }

    for (int i = 0; i < gui.elements.Count; i++) {
      var element = gui.elements[i];

      foreach (var meshFeature in _meshFeatures) {
        var elementData = meshFeature.data[i];
        var mesh = elementData.mesh;
        if (mesh == null) continue;

        foreach (var channel in enabledChannels) {
          mesh.GetUVsOrDefault(channel.Index(), _tempUvList);

          Rect[] atlasedRects;
          if (_atlasedRects != null && _atlasedRects.TryGetValue(channel, out atlasedRects)) {
            MeshUtil.RemapUvs(_tempUvList, atlasedRects[i]);
          }

          _elementMeshes[i].SetUVsAuto(channel.Index(), _tempUvList);
          _tempUvList.Clear();
        }
      }
    }
  }

  private void bakeUv3() {
    for (int i = 0; i < gui.elements.Count; i++) {
      var element = gui.elements[i];

      foreach (var meshFeature in _meshFeatures) {
        var elementData = meshFeature.data[i];
        var mesh = elementData.mesh;
        if (mesh == null) continue;

        _tempUvList.Append(mesh.vertexCount, new Vector4(0, 0, 0, i));
      }

      _elementMeshes[i].SetUVs(3, _tempUvList);
      _tempUvList.Clear();
    }
  }
}
