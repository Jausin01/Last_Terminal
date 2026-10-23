using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The operator's monitor: one view camera that sits on the active CCTV camera's lens.
// Pan: cursor at the left / right edge of the screen (FNAF style, the cursor stays free for clicks) or keys A / D,
// chosen in the settings. Tab = facility map: first a small map in a corner (the picture stays; the wheel over it zooms
// the map), Tab again = the large map over the picture, Tab again = closed. Q / E = previous / next camera,
// Esc = settings (or, when something listens to EscapeRequested - the operator's PC - that decides: the
// "close the program?" dialog). While the large map, the settings or an overlay (that dialog) are open, or the cursor is
// on the corner map, clicks don't reach the world.
// While the operator types a command (CommandConsole), the camera keys are off.
// A camera whose object is inactive is skipped (the blackjack table camera only exists during a game).
public class SecurityCameraSystem : MonoBehaviour
{
    public const string ViewCameraField = nameof(viewCamera);
    public const string CamerasField = nameof(cameras);
    public const string SettingsField = nameof(settings);
    public const string SaveScreenField = nameof(saveScreen);
    public const string StartCameraField = nameof(startCamera);

    [SerializeField] private Camera viewCamera;
    [SerializeField] private SecurityCamera[] cameras = new SecurityCamera[0];
    [SerializeField] private CameraSettings settings;
    [Tooltip("Optional: no camera input while the save screen is open.")]
    [SerializeField] private SaveScreen saveScreen;
    [Tooltip("Index (in Cameras) of the camera shown first.")]
    [SerializeField, Min(0)] private int startCamera;
    [Tooltip("Width of the screen edge that pans with the cursor (fraction of the screen).")]
    [SerializeField, Range(0.02f, 0.4f)] private float edgeSize = 0.12f;

    [Header("Input (bindings added when empty)")]
    [SerializeField] private InputAction point = new InputAction("Point", InputActionType.Value, "<Pointer>/position", expectedControlType: "Vector2");
    [SerializeField] private InputAction panKeys = new InputAction("PanKeys", InputActionType.Value, expectedControlType: "Axis");
    [SerializeField] private InputAction toggleMap = new InputAction("ToggleMap", InputActionType.Button, "<Keyboard>/tab");
    [SerializeField] private InputAction toggleSettings = new InputAction("ToggleSettings", InputActionType.Button, "<Keyboard>/escape");
    [SerializeField] private InputAction nextCamera = new InputAction("NextCamera", InputActionType.Button, "<Keyboard>/e");
    [SerializeField] private InputAction previousCamera = new InputAction("PreviousCamera", InputActionType.Button, "<Keyboard>/q");
    [SerializeField] private InputAction zoomScroll = new InputAction("ZoomScroll", InputActionType.Value, "<Mouse>/scroll/y", expectedControlType: "Axis");
    [SerializeField] private InputAction zoomKeys = new InputAction("ZoomKeys", InputActionType.Value, expectedControlType: "Axis");

    [Header("Zoom (mouse wheel toward the cursor, or + / -)")]
    [SerializeField, Min(1f)] private float maxZoom = 4f;
    [Tooltip("Zoom factor per wheel notch.")]
    [SerializeField, Min(1.01f)] private float zoomStep = 1.25f;
    [Tooltip("Keys: zoom factor per second.")]
    [SerializeField, Min(1.01f)] private float keyZoomRate = 2.5f;
    [Tooltip("How fast the view catches up with the zoom (1/s).")]
    [SerializeField] private float zoomSmoothing = 12f;

    // the map: closed, small in a corner (the picture stays usable), large over the picture
    public enum MapView { Closed, Corner, Large }

    private int active = -1;
    private float[] yaws;
    private MapView mapView;
    private bool pointerOnPanel;
    private bool settingsOpen;
    private bool typing;
    private bool overlayOpen;
    private bool locked;
    private int typingEndFrame = -1;
    // zoom of the active camera: factor (1 = the camera's own field of view) and where it looks inside that view
    private float zoom = 1f, zoomTarget = 1f;
    private Vector2 aim, aimTarget;   // deg: x = right, y = up, from the camera's own direction

    public event Action<SecurityCamera> ActiveCameraChanged;
    public event Action<MapView> MapChanged;
    public event Action<bool> SettingsToggled;
    // Esc with the settings closed: raised instead of opening the settings when someone listens (OperatorStation)
    public event Action EscapeRequested;

    public IReadOnlyList<SecurityCamera> Cameras => cameras;
    public int ActiveIndex => active;
    public SecurityCamera Active => active >= 0 ? cameras[active] : null;
    public bool IsMapOpen => mapView != MapView.Closed;
    public MapView Map => mapView;
    public bool IsSettingsOpen => settingsOpen;
    public bool IsTyping => typing;
    public bool IsOverlayOpen => overlayOpen;
    // a cutscene: the operator only watches (no camera keys, map, settings, clicks, commands)
    public bool IsLocked => locked;
    public bool BlocksWorldInput => mapView == MapView.Large || settingsOpen || typing || overlayOpen || locked || pointerOnPanel;
    // keys typed into the command line (and the Esc / Enter that closed it) are not camera keys
    private bool KeysBlocked => typing || Time.frameCount == typingEndFrame;
    // -1 = left limit, 1 = right limit
    public float PanAmount => Active != null && Active.YawRange > 0f ? Active.Yaw / Active.YawRange : 0f;
    public Camera ViewCamera => viewCamera;
    public float Zoom => zoom;

    private void Awake()
    {
        if (panKeys.bindings.Count == 0)
        {
            panKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            panKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
        }
        if (zoomKeys.bindings.Count == 0)
        {
            zoomKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/minus").With("Positive", "<Keyboard>/equals");
            zoomKeys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/numpadMinus").With("Positive", "<Keyboard>/numpadPlus");
        }
        yaws = new float[cameras.Length];
        if (viewCamera == null || settings == null || cameras.Length == 0)
        {
            Debug.LogError($"{name}: SecurityCameraSystem needs a view camera, CameraSettings and at least one SecurityCamera.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        point.Enable(); panKeys.Enable(); toggleMap.Enable(); toggleSettings.Enable(); nextCamera.Enable(); previousCamera.Enable();
        zoomScroll.Enable(); zoomKeys.Enable();
        toggleMap.performed += OnToggleMap;
        toggleSettings.performed += OnToggleSettings;
        nextCamera.performed += OnNext;
        previousCamera.performed += OnPrevious;
    }

    private void OnDisable()
    {
        toggleMap.performed -= OnToggleMap;
        toggleSettings.performed -= OnToggleSettings;
        nextCamera.performed -= OnNext;
        previousCamera.performed -= OnPrevious;
        point.Disable(); panKeys.Disable(); toggleMap.Disable(); toggleSettings.Disable(); nextCamera.Disable(); previousCamera.Disable();
        zoomScroll.Disable(); zoomKeys.Disable();
    }

    private void Start()
    {
        if (active < 0) Switch(startCamera < cameras.Length ? startCamera : 0);   // unless something chose one first (an elevator arrival)
        if (active < 0) Switch(0);
    }

    public void Switch(int index)
    {
        if (index < 0 || index >= cameras.Length || !Usable(index) || index == active) return;
        active = index;
        zoom = zoomTarget = 1f;   // a new camera starts wide
        aim = aimTarget = Vector2.zero;
        cameras[active].SetYaw(yaws[active]);
        PlaceView();
        ActiveCameraChanged?.Invoke(cameras[active]);
    }

    public void Switch(SecurityCamera cam) => Switch(Array.IndexOf(cameras, cam));

    private bool Usable(int index) => cameras[index] != null && cameras[index].gameObject.activeInHierarchy;

    public void SetMap(MapView view)
    {
        if (mapView == view) return;
        mapView = view;
        if (view != MapView.Closed) SetSettingsOpen(false);
        if (view != MapView.Corner) pointerOnPanel = false;
        MapChanged?.Invoke(view);
    }

    // the cursor is on a panel over the picture (the corner map): no pan, wheel or clicks for the world
    public void SetPointerOverPanel(bool over) => pointerOnPanel = over;

    public void SetSettingsOpen(bool open)
    {
        if (settingsOpen == open) return;
        settingsOpen = open;
        if (open) SetMap(MapView.Closed);
        SettingsToggled?.Invoke(open);
    }

    public void SetLocked(bool value)
    {
        locked = value;
        if (value) { SetMap(MapView.Closed); SetSettingsOpen(false); }
    }

    // a panel over the cameras (the PC's dialog): no clicks on the world, no camera keys
    public void SetOverlayOpen(bool open)
    {
        if (overlayOpen == open) return;
        overlayOpen = open;
        if (open) { SetMap(MapView.Closed); SetSettingsOpen(false); }
    }

    public void SetTyping(bool value)
    {
        if (typing == value) return;
        typing = value;
        if (!value) typingEndFrame = Time.frameCount;
    }

    private bool InputBlockedBySaveScreen => saveScreen != null && saveScreen.IsOpen;

    private void OnToggleMap(InputAction.CallbackContext ctx)
    {
        if (InputBlockedBySaveScreen || KeysBlocked || overlayOpen || locked) return;
        SetMap(mapView == MapView.Closed ? MapView.Corner : mapView == MapView.Corner ? MapView.Large : MapView.Closed);
    }

    private void OnToggleSettings(InputAction.CallbackContext ctx)
    {
        if (InputBlockedBySaveScreen || KeysBlocked || locked) return;
        if (!settingsOpen && EscapeRequested != null) { SetMap(MapView.Closed); EscapeRequested(); return; }
        SetSettingsOpen(!settingsOpen);
    }

    private void OnNext(InputAction.CallbackContext ctx) => Step(1);
    private void OnPrevious(InputAction.CallbackContext ctx) => Step(-1);

    private void Step(int dir)
    {
        if (InputBlockedBySaveScreen || settingsOpen || overlayOpen || locked || KeysBlocked || cameras.Length == 0) return;
        for (int k = 1; k <= cameras.Length; k++)
        {
            int i = ((active + dir * k) % cameras.Length + cameras.Length) % cameras.Length;
            if (Usable(i)) { Switch(i); return; }
        }
    }

    private void Update()
    {
        SecurityCamera cam = Active;
        if (cam == null) return;
        bool blocked = InputBlockedBySaveScreen || BlocksWorldInput;
        float pan = blocked ? 0f : ReadPan();
        if (pan != 0f)
        {
            // zoomed in: finer panning
            yaws[active] = Mathf.Clamp(yaws[active] + pan / zoom * Time.deltaTime, -cam.YawRange, cam.YawRange);
            cam.SetYaw(yaws[active]);
        }
        if (!blocked) ReadZoom(cam);
        float k = 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);
        zoom = Mathf.Lerp(zoom, zoomTarget, k);
        aim = Vector2.Lerp(aim, aimTarget, k);
        if (Mathf.Abs(zoom - zoomTarget) < 0.001f) zoom = zoomTarget;
    }

    // wheel: toward the point under the cursor (it stays under the cursor); keys: toward the middle of the picture
    private void ReadZoom(SecurityCamera cam)
    {
        bool panelOpen = mapView == MapView.Large || settingsOpen || pointerOnPanel;   // the wheel zooms / scrolls the map then
        float wheel = KeysBlocked || panelOpen ? 0f : zoomScroll.ReadValue<float>();
        float keys = KeysBlocked || panelOpen ? 0f : zoomKeys.ReadValue<float>();
        float factor = 1f;
        if (Mathf.Abs(wheel) > 0.01f) factor = Mathf.Pow(zoomStep, Mathf.Sign(wheel));
        else if (Mathf.Abs(keys) > 0.01f) factor = Mathf.Pow(keyZoomRate, keys * Time.deltaTime);
        if (factor == 1f) return;
        float next = Mathf.Clamp(zoomTarget * factor, 1f, maxZoom);
        if (Mathf.Approximately(next, zoomTarget)) return;

        Vector2 cursor = new Vector2(0.5f, 0.5f);
        if (Mathf.Abs(wheel) > 0.01f && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 p = point.ReadValue<Vector2>();
            cursor = new Vector2(Mathf.Clamp01(p.x / Screen.width), Mathf.Clamp01(p.y / Screen.height));
        }
        // the angle of the cursor from the camera's own direction stays the same after the zoom
        Vector2 before = CursorAngle(cam, zoomTarget, cursor);
        Vector2 after = CursorAngle(cam, next, cursor);
        zoomTarget = next;
        aimTarget = ClampAim(cam, aimTarget + before - after, zoomTarget);
    }

    // degrees (right, up) of a viewport point from the middle of the picture at that zoom
    private float Aspect => viewCamera.aspect > 0f ? viewCamera.aspect : 16f / 9f;

    private Vector2 CursorAngle(SecurityCamera cam, float z, Vector2 viewport)
    {
        float tanV = Mathf.Tan(cam.FieldOfView * 0.5f * Mathf.Deg2Rad) / z;
        float tanH = tanV * Aspect;
        return new Vector2(Mathf.Atan((viewport.x - 0.5f) * 2f * tanH) * Mathf.Rad2Deg,
                           Mathf.Atan((viewport.y - 0.5f) * 2f * tanV) * Mathf.Rad2Deg);
    }

    // zoomed in, the view only moves inside what the camera sees at 1x
    private Vector2 ClampAim(SecurityCamera cam, Vector2 a, float z)
    {
        float halfV = cam.FieldOfView * 0.5f;
        float halfH = Mathf.Atan(Mathf.Tan(halfV * Mathf.Deg2Rad) * Aspect) * Mathf.Rad2Deg;
        float zoomedV = Mathf.Atan(Mathf.Tan(halfV * Mathf.Deg2Rad) / z) * Mathf.Rad2Deg;
        float zoomedH = Mathf.Atan(Mathf.Tan(halfH * Mathf.Deg2Rad) / z) * Mathf.Rad2Deg;
        return new Vector2(Mathf.Clamp(a.x, -(halfH - zoomedH), halfH - zoomedH), Mathf.Clamp(a.y, -(halfV - zoomedV), halfV - zoomedV));
    }

    // deg/s
    private float ReadPan()
    {
        if (settings.Pan == CameraSettings.PanControl.Keyboard)
            return panKeys.ReadValue<float>() * settings.KeyboardPanSpeed;

        Vector2 p = point.ReadValue<Vector2>();
        if (Screen.width <= 0 || p.x < 0f || p.x > Screen.width || p.y < 0f || p.y > Screen.height) return 0f;   // cursor outside the window
        float x = p.x / Screen.width;
        float push = x < edgeSize ? -(1f - x / edgeSize) : x > 1f - edgeSize ? (x - (1f - edgeSize)) / edgeSize : 0f;
        return push * settings.MousePanSpeed;
    }

    private void LateUpdate() => PlaceView();

    private void PlaceView()
    {
        SecurityCamera cam = Active;
        if (cam == null) return;
        cam.GetView(out Vector3 pos, out Quaternion rot);
        // zoom: narrower field of view, turned toward where it zooms (inside the camera's own view)
        rot = rot * Quaternion.Euler(-aim.y, aim.x, 0f);
        viewCamera.transform.SetPositionAndRotation(pos, rot);
        float tanV = Mathf.Tan(cam.FieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(zoom, 1f);
        viewCamera.fieldOfView = 2f * Mathf.Atan(tanV) * Mathf.Rad2Deg;
    }
}
