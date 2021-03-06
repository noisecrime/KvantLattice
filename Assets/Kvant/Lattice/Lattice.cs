﻿//
// Lattice - fractal-deformed lattice renderer
//
using UnityEngine;
using UnityEngine.Rendering;

namespace Kvant
{
    [ExecuteInEditMode, AddComponentMenu("Kvant/Lattice")]
    public partial class Lattice : MonoBehaviour
    {
        #region Parameters Exposed To Editor

        [SerializeField]
        int _columns = 100;

        [SerializeField]
        int _rows = 100;

        [SerializeField]
        Vector2 _size = Vector2.one * 10;

        [SerializeField]
        Vector2 _noiseOffset = Vector2.zero;

        [SerializeField, Range(0, 1)]
        float _noiseFrequency = 2.0f;

        [SerializeField, Range(1, 5)]
        int _noiseDepth = 4;

        [SerializeField]
        float _noiseClampMin = -1.5f;

        [SerializeField]
        float _noiseClampMax = 1.5f;

        [SerializeField]
        float _noiseElevation = 0.5f;

        [SerializeField, Range(0, 1)]
        float _noiseWarp = 0.1f;

        [SerializeField]
        Color _surfaceColor = Color.white;

        [SerializeField, ColorUsage(true, true, 0, 8, 0.125f, 3)]
        Color _lineColor = new Color(0, 0, 0, 0.4f);

        [SerializeField, Range(0, 1)]
        float _metallic = 0.5f;

        [SerializeField, Range(0, 1)]
        float _smoothness = 0.5f;

        [SerializeField]
        ShadowCastingMode _castShadows;

        [SerializeField]
        bool _receiveShadows = false;

        [SerializeField]
        Texture2D _albedoMap;

        [SerializeField]
        Texture2D _normalMap;

        [SerializeField]
        Texture2D _occlusionMap;

        [SerializeField, Range(0, 1)]
        float _occlusionStrength;

        [SerializeField]
        float _mapScale = 1.0f;

        [SerializeField]
        bool _debug;

        #endregion

        #region Public Properties

        public int columns {
            get { return _columns; }
        }

        public int rows {
            get { return _totalRows; }
        }

        public Vector2 size {
            get { return _size; }
            set { _size = value; }
        }

        public Vector2 noiseOffset {
            get { return _noiseOffset; }
            set { _noiseOffset = value; }
        }

        public float noiseFrequency {
            get { return _noiseFrequency; }
            set { _noiseFrequency = value; }
        }

        public int noiseDepth {
            get { return _noiseDepth; }
            set { _noiseDepth = value; }
        }

        public float noiseClampMin {
            get { return _noiseClampMin; }
            set { _noiseClampMin = value; }
        }

        public float noiseClampMax {
            get { return _noiseClampMax; }
            set { _noiseClampMax = value; }
        }

        public float noiseElevation {
            get { return _noiseElevation; }
            set { _noiseElevation = value; }
        }

        public float noiseWarp {
            get { return _noiseWarp; }
            set { _noiseWarp = value; }
        }

        public Color surfaceColor {
            get { return _surfaceColor; }
            set { _surfaceColor = value; }
        }

        public Color lineColor {
            get { return _lineColor; }
            set { _lineColor = value; }
        }

        public float metallic {
            get { return _metallic; }
            set { _metallic = value; }
        }

        public float smoothness {
            get { return _smoothness; }
            set { _smoothness = value; }
        }

        public ShadowCastingMode shadowCastingMode {
            get { return _castShadows; }
            set { _castShadows = value; }
        }

        public bool receiveShadows {
            get { return _receiveShadows; }
            set { _receiveShadows = value; }
        }

        public Texture2D albedoMap {
            get { return _albedoMap; }
            set { _albedoMap = value; }
        }

        public Texture2D normalMap {
            get { return _normalMap; }
            set { _normalMap = value; }
        }

        public Texture2D occlusionMap {
            get { return _occlusionMap; }
            set { _occlusionMap = value; }
        }

        public float mapScale {
            get { return _mapScale; }
            set { _mapScale = value; }
        }

        #endregion

        #region Shaders And Materials

        [SerializeField] Shader _kernelShader;
        [SerializeField] Shader _surfaceShader;
        [SerializeField] Shader _lineShader;
        [SerializeField] Shader _debugShader;

        Material _kernelMaterial;
        Material _surfaceMaterial1;
        Material _surfaceMaterial2;
        Material _lineMaterial;
        Material _debugMaterial;

        #endregion

        #region Private Variables And Objects

        int _rowsPerSegment;
        int _totalRows;

        RenderTexture _positionBuffer;
        RenderTexture _normalBuffer1;
        RenderTexture _normalBuffer2;

        BulkMesh _bulkMesh;
        bool _needsReset = true;

        #endregion

        #region Private Properties

        void UpdateColumnAndRowCounts()
        {
            // Sanitize the numbers.
            _columns = Mathf.Clamp(_columns, 4, 4096);
            _rows = Mathf.Clamp(_rows, 4, 4096);

            // Total number of vertices.
            var total_vc = (_columns + 1) * (_rows + 1) * 6;

            // Number of segments.
            var segments = total_vc / 65000 + 1;

            _rowsPerSegment = segments > 1 ? (_rows / segments) / 2 * 2 : _rows;
            _totalRows = _rowsPerSegment * segments;
        }

        #endregion

        #region Resource Management

        public void NotifyConfigChange()
        {
            _needsReset = true;
        }

        Material CreateMaterial(Shader shader)
        {
            var material = new Material(shader);
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        RenderTexture CreateBuffer()
        {
            var width = (_columns + 1) * 2;
            var height = _totalRows + 1;
            var buffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
            buffer.hideFlags = HideFlags.DontSave;
            buffer.filterMode = FilterMode.Point;
            buffer.wrapMode = TextureWrapMode.Repeat;
            return buffer;
        }

        void UpdateKernelShader()
        {
            var m = _kernelMaterial;

            m.SetVector("_Size", _size);
            m.SetVector("_Noise", new Vector4(_noiseFrequency, _noiseOffset.x, _noiseOffset.y));
            m.SetVector("_Displace", new Vector4(_noiseElevation, _noiseClampMin, _noiseClampMax, _noiseElevation * _noiseWarp));

            if (_noiseWarp > 0.0f)
                m.EnableKeyword("ENABLE_WARP");
            else
                m.DisableKeyword("ENABLE_WARP");

            for (var i = 1; i <= 5; i++) {
                if (i == _noiseDepth)
                    m.EnableKeyword("DEPTH" + i);
                else
                    m.DisableKeyword("DEPTH" + i);
            }
        }

        void UpdateSurfaceMaterial(Material m)
        {
            m.SetColor("_Color", _surfaceColor);
            m.SetVector("_PbrParams", new Vector2(_metallic, _smoothness));

            if (_albedoMap) {
                m.SetTexture("_MainTex", _albedoMap);
                m.EnableKeyword("_ALBEDOMAP");
            } else {
                m.DisableKeyword("_ALBEDOMAP");
            }

            if (_normalMap) {
                m.SetTexture("_BumpMap", _normalMap);
                m.EnableKeyword("_NORMALMAP");
            } else {
                m.DisableKeyword("_NORMALMAP");
            }

            if (_occlusionMap) {
                m.SetTexture("_OcclusionMap", _occlusionMap);
                m.SetFloat("_OcclusionStrength", _occlusionStrength);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            else {
                m.DisableKeyword("_OCCLUSIONMAP");
            }

            m.SetVector("_MapParams", new Vector4(_noiseOffset.x, 0, _noiseOffset.y, _mapScale));
        }

        void ResetResources()
        {
            UpdateColumnAndRowCounts();

            // Mesh object.
            if (_bulkMesh == null)
                _bulkMesh = new BulkMesh(_columns, _rowsPerSegment, _totalRows);
            else
                _bulkMesh.Rebuild(_columns, _rowsPerSegment, _totalRows);

            // Displacement buffers.
            if (_positionBuffer) DestroyImmediate(_positionBuffer);
            if (_normalBuffer1)  DestroyImmediate(_normalBuffer1);
            if (_normalBuffer2)  DestroyImmediate(_normalBuffer2);

            _positionBuffer = CreateBuffer();
            _normalBuffer1  = CreateBuffer();
            _normalBuffer2  = CreateBuffer();

            // Shader materials.
            if (!_kernelMaterial)   _kernelMaterial   = CreateMaterial(_kernelShader);
            if (!_surfaceMaterial1) _surfaceMaterial1 = CreateMaterial(_surfaceShader);
            if (!_surfaceMaterial2) _surfaceMaterial2 = CreateMaterial(_surfaceShader);
            if (!_lineMaterial)     _lineMaterial     = CreateMaterial(_lineShader);
            if (!_debugMaterial)    _debugMaterial    = CreateMaterial(_debugShader);

            // Set buffer references.
            _surfaceMaterial1.SetTexture("_PositionTex", _positionBuffer);
            _surfaceMaterial2.SetTexture("_PositionTex", _positionBuffer);
            _lineMaterial    .SetTexture("_PositionTex", _positionBuffer);
            _surfaceMaterial1.SetTexture("_NormalTex",   _normalBuffer1);
            _surfaceMaterial2.SetTexture("_NormalTex",   _normalBuffer2);

            _needsReset = false;
        }

        #endregion

        #region MonoBehaviour Functions

        void Reset()
        {
            _needsReset = true;
        }

        void OnDestroy()
        {
            if (_bulkMesh != null) _bulkMesh.Release();
            if (_positionBuffer)   DestroyImmediate(_positionBuffer);
            if (_normalBuffer1)    DestroyImmediate(_normalBuffer1);
            if (_normalBuffer2)    DestroyImmediate(_normalBuffer2);
            if (_kernelMaterial)   DestroyImmediate(_kernelMaterial);
            if (_surfaceMaterial1) DestroyImmediate(_surfaceMaterial1);
            if (_surfaceMaterial2) DestroyImmediate(_surfaceMaterial2);
            if (_lineMaterial)     DestroyImmediate(_lineMaterial);
            if (_debugMaterial)    DestroyImmediate(_debugMaterial);
        }

        void LateUpdate()
        {
            if (_needsReset) ResetResources();

            // Execute the kernel shaders.
            UpdateKernelShader();
            Graphics.Blit(null, _positionBuffer, _kernelMaterial, 0);
            Graphics.Blit(_positionBuffer, _normalBuffer1, _kernelMaterial, 1);
            Graphics.Blit(_positionBuffer, _normalBuffer2, _kernelMaterial, 2);

            // Update the display materials.
            UpdateSurfaceMaterial(_surfaceMaterial1);
            UpdateSurfaceMaterial(_surfaceMaterial2);
            _lineMaterial.SetColor("_Color", _lineColor);

            // Fill segments with the bulk mesh.
            var mesh = _bulkMesh.mesh;
            var uv = new Vector2(0.5f / _positionBuffer.width, 0);
            var offs = new MaterialPropertyBlock();
            var p = transform.position;
            var r = transform.rotation;

            for (var i = 0; i < _totalRows; i += _rowsPerSegment)
            {
                uv.y = (0.5f + i) / _positionBuffer.height;
                offs.AddVector("_UVOffset", uv);

                Graphics.DrawMesh(mesh, p, r, _surfaceMaterial1, 0, null, 0, offs, _castShadows, _receiveShadows);
                Graphics.DrawMesh(mesh, p, r, _surfaceMaterial2, 0, null, 1, offs, _castShadows, _receiveShadows);

                if (_lineColor.a > 0.0f)
                    Graphics.DrawMesh(mesh, p, r, _lineMaterial, 0, null, 2, offs, false, false);
            }
        }

        void OnGUI()
        {
            if (_debug && Event.current.type.Equals(EventType.Repaint) && _debugMaterial)
            {
                var w = 64;
                var r1 = new Rect(0 * w, 0, w, w);
                var r2 = new Rect(1 * w, 0, w, w);
                var r3 = new Rect(2 * w, 0, w, w);
                if (_positionBuffer) Graphics.DrawTexture(r1, _positionBuffer, _debugMaterial);
                if (_normalBuffer1)  Graphics.DrawTexture(r2, _normalBuffer1,  _debugMaterial);
                if (_normalBuffer2)  Graphics.DrawTexture(r3, _normalBuffer2,  _debugMaterial);
            }
        }

        #endregion
    }
}
