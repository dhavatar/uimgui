using ImGuiNET;
using System.Linq;
using UImGui.Assets;
using UImGui.Events;
using UImGui.Platform;
using UImGui.Renderer;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UImGui
{
    // TODO: Check Multithread run.
    public class UImGui : MonoBehaviour
    {
        private Context _context;
        private IRenderer _renderer;
        private IPlatform _platform;
#if !HAS_HDRP && !HAS_URP
        private CommandBuffer _renderCommandBuffer;
#endif

#if HAS_URP
        private RenderImGui.Settings _settings;
#endif

        [SerializeField] private bool _singletonMode;

        [Tooltip("If checked, will always try to find the main camera and ignors the camera setting below.")]
        [SerializeField] private bool _useMainCamera;
        [SerializeField] private Camera _camera = null;

        [SerializeField, HideInInspector] private RenderImGui _renderFeature = null;

        [SerializeField] private RenderType _rendererType = RenderType.Mesh;
        [SerializeField] private InputType _platformType = InputType.InputManager;

        [Tooltip("Null value uses default imgui.ini file.")]
        [SerializeField] private IniSettingsAsset _iniSettings = null;

        [Header("Configuration")]

        [SerializeField]
        private UIOConfig _initialConfiguration = new UIOConfig
        {
            ImGuiConfig = ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.DockingEnable,

            DoubleClickTime = 0.30f,
            DoubleClickMaxDist = 6.0f,

            DragThreshold = 6.0f,

            KeyRepeatDelay = 0.250f,
            KeyRepeatRate = 0.050f,

            FontGlobalScale = 1.0f,
            FontAllowUserScaling = false,

            DisplayFramebufferScale = Vector2.one,

            MouseDrawCursor = false,
            TextCursorBlink = false,

            ResizeFromEdges = true,
            MoveFromTitleOnly = true,
            ConfigMemoryCompactTimer = 1f,
        };

        [SerializeField] private FontInitializerEvent _fontCustomInitializer = new FontInitializerEvent();
        [SerializeField] private FontAtlasConfigAsset _fontAtlasConfiguration = null;

        [Header("Customization")]
        [SerializeField] private ShaderResourcesAsset _shaders = null;
        [SerializeField] private StyleAsset _style = null;
        [SerializeField] private CursorShapesAsset _cursorShapes = null;
        [SerializeField] private bool _doGlobalEvents = true; // Do global/default Layout event too.

        private bool _isChangingCamera = false;

        private static UImGui _instance;
#region Events
        public event System.Action<UImGui> Layout;
        public event System.Action<UImGui> OnInitialize;
        public event System.Action<UImGui> OnDeinitialize;
#endregion

        public void Reload()
        {
            OnDisable();
            OnEnable();
        }

        public void SetUserData(System.IntPtr userDataPtr)
        {
            _initialConfiguration.UserData = userDataPtr;
            ImGuiIOPtr io = ImGui.GetIO();
            _initialConfiguration.ApplyTo(io);
        }

        public void SetCamera(Camera camera)
        {
            if (_useMainCamera)
            {
                Debug.Log($"Trying to change camera, but currently set to always use main camera");
                return;
            }

            if (camera == null)
            {
                enabled = false;
                throw new System.Exception($"Fail: {camera} is null.");
            }

            if(camera == _camera)
            {
                Debug.LogWarning($"Trying to change to same camera. Camera: {camera}", camera);
                return;
            }

            _camera = camera;
            _isChangingCamera = true;
        }

        private void Awake()
        {
            if (_singletonMode)
            {
                if (_instance != null)
                {
                    Destroy(gameObject);
                    return;
                }

                _instance = this;
                DontDestroyOnLoad(gameObject);
            }

            _context = UImGuiUtility.CreateContext();

#if HAS_URP
            _settings = new RenderImGui.Settings();
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset == null)
                {
                    Debug.LogError("Somehow could not grab the render pipeline.", gameObject);
                    return;
                }
            }

            if (urpAsset.rendererDataList == null)
            {
                Debug.LogError("There's no render data on the current render pipeline.", gameObject);
                return;
            }

            // Find and store the first instance of the RenderImGui in the render list
            foreach (var renderData in urpAsset.rendererDataList)
            {
                _renderFeature = renderData.rendererFeatures.Where(x => x is RenderImGui)
                    .FirstOrDefault() as RenderImGui;

                if (_renderFeature != null)
                {
                    _renderFeature.settings = _settings;
                    break;
                }
            }
#endif
        }

        private void OnDestroy()
        {
            UImGuiUtility.DestroyContext(_context);
        }

        private void OnEnable()
        {
            void Fail(string reason)
            {
                enabled = false;
                throw new System.Exception($"Failed to start: {reason}.");
            }

            if (_useMainCamera)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                Fail(nameof(_camera));
            }

            if (_renderFeature == null && RenderUtility.IsUsingURP())
            {
                Fail(nameof(_renderFeature));
            }

            if (RenderUtility.IsUsingURP())
            {
#if HAS_URP
                _renderFeature.Camera = _camera;
#endif
            }
            else if (!RenderUtility.IsUsingHDRP())
            {
#if !HAS_HDRP && !HAS_URP
                _renderCommandBuffer = RenderUtility.GetCommandBuffer(Constants.UImGuiCommandBuffer);
                _camera.AddCommandBuffer(CameraEvent.AfterEverything, _renderCommandBuffer);
#endif
            }

            UImGuiUtility.SetCurrentContext(_context);

            ImGuiIOPtr io = ImGui.GetIO();

            _initialConfiguration.ApplyTo(io);
            _style?.ApplyTo(ImGui.GetStyle());

            _context.TextureManager.BuildFontAtlas(io, _fontAtlasConfiguration, _fontCustomInitializer);
            _context.TextureManager.Initialize(io);

            IPlatform platform = PlatformUtility.Create(_platformType, _cursorShapes, _iniSettings);
            SetPlatform(platform, io);
            if (_platform == null)
            {
                Fail(nameof(_platform));
            }

            SetRenderer(RenderUtility.Create(_rendererType, _shaders, _context.TextureManager), io);
            if (_renderer == null)
            {
                Fail(nameof(_renderer));
            }

#if HAS_URP
            _settings.renderer = _renderer;
#endif

            if (_doGlobalEvents)
            {
                UImGuiUtility.DoOnInitialize(this);
            }
            OnInitialize?.Invoke(this);
        }

        private void OnDisable()
        {
            UImGuiUtility.SetCurrentContext(_context);
            ImGuiIOPtr io = ImGui.GetIO();

            SetRenderer(null, io);
            SetPlatform(null, io);

            UImGuiUtility.SetCurrentContext(null);

            if (_context != null)
            {
                _context.TextureManager.Shutdown();
                _context.TextureManager.DestroyFontAtlas(io);
            }

            if (RenderUtility.IsUsingURP())
            {
                if (_renderFeature != null)
                {
#if HAS_URP
                    _renderFeature.Camera = null;
#endif
                }
            }
            else if(!RenderUtility.IsUsingHDRP())
            {
#if !HAS_HDRP && !HAS_URP
                if (_camera != null)
                {
                    _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _renderCommandBuffer);
                }
#endif
            }

            if (_doGlobalEvents)
            {
                UImGuiUtility.DoOnDeinitialize(this);
            }
            OnDeinitialize?.Invoke(this);
        }

        private void Update()
        {
            if (RenderUtility.IsUsingHDRP())
                return; // skip update call in hdrp

            // If the camera is null, the scene probably changed and UImGui should either try to find
            // the camera again or disable itself depending on the settings.
            if (_camera == null)
            {
                if (_useMainCamera)
                {
                    _camera = Camera.main;
                }

                // If the camera can't be found, disable this. Can be re-enabled by another script.
                if (_camera == null)
                {
                    Debug.LogWarning("Camera is null! Disabling UImGui.");
                    OnDisable();
                    return;
                }
            }
            
            // If the context becomes null, then either Unity hot reloaded or something else went wrong.
            if (_context == null)
            {
                Debug.LogWarning("Context is null for UImGui! Disabling script.");
                OnDisable();
                return;
            }

            DoUpdate();
        }

        internal void DoUpdate()
        {
            UImGuiUtility.SetCurrentContext(_context);
            ImGuiIOPtr io = ImGui.GetIO();

            Constants.PrepareFrameMarker.Begin(this);
            _context.TextureManager.PrepareFrame(io);
            _platform.PrepareFrame(io, _camera.pixelRect);
            ImGui.NewFrame();
#if UIMGUI_USE_IMGUIZMO
            ImGuizmoNET.ImGuizmo.BeginFrame();
#endif
            Constants.PrepareFrameMarker.End();

            Constants.LayoutMarker.Begin(this);
            try
            {
                if (_doGlobalEvents)
                {
                    UImGuiUtility.DoLayout(this);
                }

                Layout?.Invoke(this);
            }
            finally
            {
                ImGui.Render();
                Constants.LayoutMarker.End();
            }
            
            Constants.DrawListMarker.Begin(this);
#if !HAS_HDRP && !HAS_URP
            _renderCommandBuffer.Clear();
#endif
            //_renderer.RenderDrawLists(buffer, ImGui.GetDrawData());
            Constants.DrawListMarker.End();

            if (_isChangingCamera)
            {
                _isChangingCamera = false;
                Reload();
            }
        }

        private void SetRenderer(IRenderer renderer, ImGuiIOPtr io)
        {
            _renderer?.Shutdown(io);
            _renderer = renderer;
            _renderer?.Initialize(io);
        }

        private void SetPlatform(IPlatform platform, ImGuiIOPtr io)
        {
            _platform?.Shutdown(io);
            _platform = platform;
            _platform?.Initialize(io, _initialConfiguration, "Unity " + _platformType.ToString());
        }
    }
}