using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpiePettingMod
{
    public class ExpiePettingController : MonoBehaviour
    {
        public static ExpiePettingController Instance { get; private set; } = null!;

        private Limb? _grabbedLimb;
        private bool _isDragging;
        private Vector2 _grabOffset;
        private Vector2 _lastMouseWorldPos;
        private Vector2 _lastMouseScreenPos;
        private float _stretchSoundCooldown;
        private float _happyPetTimer;
        private float _painPetTimer;
        private float _lastLineTriggerTime;
        private bool _grabbedLimbSimulated;
        private float _lastActivePetTime;
        private bool _isPettingHealthy;
        private List<Limb> _simulatedLimbGroup = new List<Limb>();

        private struct JointAnchorState
        {
            public HingeJoint2D joint;
            public Rigidbody2D originalConnectedBody;
            public Vector2 originalConnectedAnchor;
        }
        private List<JointAnchorState> _activeJointAnchors = new List<JointAnchorState>();

        private float _pettingSaturation;
        private Dictionary<Body, float> _bodySatiety = new Dictionary<Body, float>();
        private HashSet<Body> _permanentlyIndifferentBodies = new HashSet<Body>();
        private Dictionary<Body, float> _lastActivePetTimePerBody = new Dictionary<Body, float>();
        private Dictionary<Body, bool> _isPettingHealthyPerBody = new Dictionary<Body, bool>();

        public bool IsPettingRecently => IsPettingRecentlyFor(null);
        public bool IsPettingHealthy => IsPettingHealthyFor(null);
        public float PettingSaturation => _pettingSaturation;

        public bool IsPettingRecentlyFor(Body? body)
        {
            if (body == null)
            {
                float newestTime = 0f;
                foreach (float t in _lastActivePetTimePerBody.Values)
                {
                    if (t > newestTime) newestTime = t;
                }
                return (Time.time - newestTime) < 1.5f;
            }
            if (_lastActivePetTimePerBody.TryGetValue(body, out float lastTime))
            {
                return (Time.time - lastTime) < 1.5f;
            }
            return false;
        }

        public bool IsPettingHealthyFor(Body? body)
        {
            if (body == null)
            {
                return _isPettingHealthy;
            }
            if (_isPettingHealthyPerBody.TryGetValue(body, out bool healthy))
            {
                return healthy;
            }
            return false;
        }

        public float GetPettingSaturation(Body? body)
        {
            if (body == null) return 0f;
            if (_permanentlyIndifferentBodies.Contains(body)) return 100f;
            if (_bodySatiety.TryGetValue(body, out float sat))
            {
                return sat;
            }
            return 0f;
        }

        public bool IsPermanentlyIndifferent(Body? body)
        {
            return body != null && _permanentlyIndifferentBodies.Contains(body);
        }

        private void CleanupDestroyedBodies()
        {
            _permanentlyIndifferentBodies.RemoveWhere(b => b == null || b.gameObject == null);

            List<Body> toRemove = new List<Body>();
            foreach (var kvp in _bodySatiety)
            {
                if (kvp.Key == null || kvp.Key.gameObject == null)
                {
                    toRemove.Add(kvp.Key!);
                }
            }
            foreach (Body b in toRemove)
            {
                _bodySatiety.Remove(b);
            }

            // Cleanup new per-body dictionaries
            List<Body> petTimeKeysToRemove = new List<Body>();
            foreach (var kvp in _lastActivePetTimePerBody)
            {
                if (kvp.Key == null || kvp.Key.gameObject == null)
                {
                    petTimeKeysToRemove.Add(kvp.Key!);
                }
            }
            foreach (Body b in petTimeKeysToRemove)
            {
                _lastActivePetTimePerBody.Remove(b);
            }

            List<Body> petHealthKeysToRemove = new List<Body>();
            foreach (var kvp in _isPettingHealthyPerBody)
            {
                if (kvp.Key == null || kvp.Key.gameObject == null)
                {
                    petHealthKeysToRemove.Add(kvp.Key!);
                }
            }
            foreach (Body b in petHealthKeysToRemove)
            {
                _isPettingHealthyPerBody.Remove(b);
            }
        }

        private void DecayAllSatieties(Body? activeBody)
        {
            List<Body> keys = new List<Body>(_bodySatiety.Keys);
            foreach (Body b in keys)
            {
                if (b == null || b == activeBody || _permanentlyIndifferentBodies.Contains(b))
                {
                    continue;
                }
                float sat = _bodySatiety[b];
                sat = Mathf.Max(0f, sat - Time.deltaTime * Plugin.Cfg.PettingDecayRate.Value);
                _bodySatiety[b] = sat;
            }
        }

        private static readonly List<string> HappyLines = new List<string>
        {
            "*sighs happily*",
            "*feels comforted*",
            "*purrs softly*",
            "*coos*",
            "Thank you...",
            "*leans into your hand*",
            "*rests quietly*",
            "*soft smile*",
            "*feels safe*"
        };

        private static readonly List<string> PainLines = new List<string>
        {
            "AHHH! Please stop!",
            "It hurts so much!",
            "Don't touch that part!",
            "*screams in pain*",
            "Please... no...",
            "*whimpers in agony*",
            "Ow! That hurts!"
        };

        // Expose interaction state to block clawing/punching
        public bool IsInteracting => _isDragging || (Plugin.Cfg.ModifierKey.Value == KeyCode.None ? Input.GetMouseButton(0) : Input.GetKey(Plugin.Cfg.ModifierKey.Value));

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _lastMouseScreenPos = Input.mousePosition;
        }

        private void Update()
        {
            CleanupDestroyedBodies();

            bool altPressed = Plugin.Cfg.ModifierKey.Value == KeyCode.None ? Input.GetMouseButton(0) : Input.GetKey(Plugin.Cfg.ModifierKey.Value);
            bool isPettingThisFrame = false;
            Body? activeBody = null;

            if (altPressed)
            {
                // Active petting and hovered limb detection
                isPettingThisFrame = UpdateHoverAndPet(out Limb? hoveredLimb, out activeBody);
            }
            else
            {
                ReleaseGrab();
            }

            // Update satiety for the active body
            if (activeBody != null)
            {
                float currentSat = 0f;
                if (!_permanentlyIndifferentBodies.Contains(activeBody))
                {
                    _bodySatiety.TryGetValue(activeBody, out currentSat);
                    if (isPettingThisFrame)
                    {
                        currentSat = Mathf.Min(100f, currentSat + Time.deltaTime * Plugin.Cfg.PettingSaturationRate.Value);
                        if (currentSat >= 100f)
                        {
                            _permanentlyIndifferentBodies.Add(activeBody);
                        }
                    }
                    else
                    {
                        currentSat = Mathf.Max(0f, currentSat - Time.deltaTime * Plugin.Cfg.PettingDecayRate.Value);
                    }
                    _bodySatiety[activeBody] = currentSat;
                }
                else
                {
                    currentSat = 100f;
                }

                _pettingSaturation = currentSat;
            }
            else
            {
                _pettingSaturation = 0f;
            }

            // Decay other bodies (excluding activeBody and permanently indifferent ones)
            DecayAllSatieties(activeBody);

            // Update stable joint anchors to follow moving animated parent bodies (like the torso)
            UpdateJointAnchors();

            _lastMouseScreenPos = Input.mousePosition;
        }

        private Body? FindActiveBody()
        {
            // Find all Body components in the scene
            Body[] bodies = FindObjectsOfType<Body>();
            if (bodies == null || bodies.Length == 0) return null;

            // Filter out the player's Body to always target the Expie/Subject
            Body? playerBody = (PlayerCamera.main != null) ? PlayerCamera.main.body : null;
            foreach (Body b in bodies)
            {
                if (b != playerBody)
                {
                    return b;
                }
            }

            // Fallback to playerBody if it's the only one found
            return playerBody ?? bodies[0];
        }

        private Limb? DetectLimbAt(Vector2 mouseWorld)
        {
            // Find all Body components in the scene
            Body[] bodies = FindObjectsOfType<Body>();
            if (bodies == null || bodies.Length == 0) return null;

            // First attempt: Physics Raycast (works when limbs are simulated / ragdoll)
            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero);
            if (hit.collider != null)
            {
                Limb limb = hit.collider.GetComponent<Limb>();
                if (limb == null)
                {
                    limb = hit.collider.GetComponentInParent<Limb>();
                }
                if (limb != null && !limb.dismembered)
                {
                    return limb;
                }
            }

            // Second attempt (Fallback): SpriteRenderer 2D Bounds Check across all bodies
            Limb? closestLimb = null;
            float closestDist = float.MaxValue;

            foreach (Body body in bodies)
            {
                if (body == null || body.limbs == null) continue;

                foreach (Limb limb in body.limbs)
                {
                    if (limb == null || limb.dismembered) continue;

                    SpriteRenderer sr = limb.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Bounds bounds = sr.bounds;
                        // Expand bounds slightly to make hovering/touching much more forgiving
                        bounds.Expand(0.18f);

                        // Pure 2D bounds check to prevent flat 2D sprite Z-axis precision bugs
                        if (mouseWorld.x >= bounds.min.x && mouseWorld.x <= bounds.max.x &&
                            mouseWorld.y >= bounds.min.y && mouseWorld.y <= bounds.max.y)
                        {
                            float dist = Vector2.Distance(mouseWorld, limb.transform.position);
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                closestLimb = limb;
                            }
                        }
                    }
                }
            }
            return closestLimb;
        }

        private Limb? DetectLimbAt(Vector2 mouseWorld, Body body)
        {
            if (body == null) return DetectLimbAt(mouseWorld);

            // First attempt: Physics Raycast
            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero);
            if (hit.collider != null)
            {
                Limb limb = hit.collider.GetComponent<Limb>();
                if (limb == null)
                {
                    limb = hit.collider.GetComponentInParent<Limb>();
                }
                if (limb != null && !limb.dismembered && limb.body == body)
                {
                    return limb;
                }
            }

            // Second attempt: 2D bounds check
            if (body.limbs != null)
            {
                Limb? closestLimb = null;
                float closestDist = float.MaxValue;

                foreach (Limb limb in body.limbs)
                {
                    if (limb == null || limb.dismembered) continue;

                    SpriteRenderer sr = limb.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Bounds bounds = sr.bounds;
                        bounds.Expand(0.18f);

                        if (mouseWorld.x >= bounds.min.x && mouseWorld.x <= bounds.max.x &&
                            mouseWorld.y >= bounds.min.y && mouseWorld.y <= bounds.max.y)
                        {
                            float dist = Vector2.Distance(mouseWorld, limb.transform.position);
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                closestLimb = limb;
                            }
                        }
                    }
                }
                return closestLimb;
            }

            return null;
        }

        private bool UpdateHoverAndPet(out Limb? hoveredLimb, out Body? activeBody)
        {
            hoveredLimb = null;
            activeBody = null;

            Camera activeCam = null!;
            if (PlayerCamera.main != null)
            {
                activeCam = PlayerCamera.main.GetComponent<Camera>();
            }
            if (activeCam == null)
            {
                activeCam = Camera.main;
            }
            if (activeCam == null) return false;

            Vector3 mouseWorld3D = activeCam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mouseWorld = new Vector2(mouseWorld3D.x, mouseWorld3D.y);

            // 1. Determine hovered limb and active body
            if (_isDragging && _grabbedLimb != null)
            {
                hoveredLimb = _grabbedLimb;
                activeBody = _grabbedLimb.body;
            }
            else
            {
                hoveredLimb = DetectLimbAt(mouseWorld);
                if (hoveredLimb != null)
                {
                    activeBody = hoveredLimb.body;
                }
            }

            if (activeBody == null)
            {
                _lastMouseWorldPos = mouseWorld;
                return false;
            }

            bool activelyPettingHealthyThisFrame = false;

            // 2. Handle Petting (Requires only Alt, doesn't require Click)
            if (hoveredLimb != null)
            {
                float screenDelta = Vector2.Distance(Input.mousePosition, _lastMouseScreenPos);
                float mouseSpeed = Time.deltaTime > 0f ? (screenDelta / Time.deltaTime) : 0f;
                // Require active mouse movement (stroking speed above threshold) to register petting
                if (mouseSpeed >= Plugin.Cfg.MinPettingSpeed.Value)
                {
                    bool wasPettingRecently = IsPettingRecentlyFor(activeBody);
                    _lastActivePetTimePerBody[activeBody] = Time.time;
                    _lastActivePetTime = Time.time; // Keep global updated as fallback
                    
                    bool isHealthy = (hoveredLimb.skinHealth >= 95f);
                    _isPettingHealthyPerBody[activeBody] = isHealthy;
                    _isPettingHealthy = isHealthy; // Keep global updated as fallback

                    if (!wasPettingRecently)
                    {
                        // Instant physical feedback upon starting petting
                        if (isHealthy)
                        {
                            if (hoveredLimb.body != null && hoveredLimb.body.conscious)
                            {
                                Sound.Play("exert" + UnityEngine.Random.Range(1, 5), hoveredLimb.rb.position, volume: 0.08f, pitchShift: true, pitch: 1.4f);
                            }
                        }
                        else
                        {
                            TriggerPainReaction(hoveredLimb);
                        }
                    }

                    // Trigger gentle organic micro-wiggle tremor on the hovered limb (only if simulated!)
                    if (hoveredLimb.rb.simulated)
                    {
                        hoveredLimb.rb.AddForce(UnityEngine.Random.insideUnitCircle * 0.4f * hoveredLimb.rb.mass, ForceMode2D.Impulse);
                    }

                    // Apply petting vitals updates
                    if (hoveredLimb.skinHealth >= 95f)
                    {
                        // Case A: Healthy Petting!
                        activelyPettingHealthyThisFrame = true;

                        // Calculate petting comfort efficiency based on current satiety level
                        float efficiency = 1f;
                        float currentSat = GetPettingSaturation(activeBody);
                        if (currentSat >= 50f)
                        {
                            efficiency = Mathf.Clamp01(1f - (currentSat - 50f) / 50f);
                        }

                        hoveredLimb.pain = Mathf.Max(0f, hoveredLimb.pain - Time.deltaTime * 7.5f * efficiency);
                        activeBody.happiness = Mathf.Min(100f, activeBody.happiness + Time.deltaTime * Plugin.Cfg.HappinessGainRate.Value * efficiency);
                        activeBody.traumaAmount = Mathf.Max(0f, activeBody.traumaAmount - Time.deltaTime * Plugin.Cfg.StressDecreaseRate.Value * efficiency);

                        if (efficiency > 0f)
                        {
                            _happyPetTimer += Time.deltaTime * efficiency;
                            if (_happyPetTimer >= UnityEngine.Random.Range(2.0f, 3.5f))
                            {
                                _happyPetTimer = 0f;
                                TriggerHappyReaction(hoveredLimb);
                            }
                        }
                    }
                    else
                    {
                        // Case B: Painful touch on skin wounds/burns!
                        hoveredLimb.pain = Mathf.Min(100f, hoveredLimb.pain + Time.deltaTime * Plugin.Cfg.PainIncreaseRate.Value);
                        activeBody.shock = Mathf.Min(100f, activeBody.shock + Time.deltaTime * 7.5f);
                        activeBody.happiness = Mathf.Max(-100f, activeBody.happiness - Time.deltaTime * 12f);

                        _painPetTimer += Time.deltaTime;
                        if (_painPetTimer >= UnityEngine.Random.Range(0.8f, 1.5f))
                        {
                            _painPetTimer = 0f;
                            TriggerPainReaction(hoveredLimb);
                        }
                    }
                }
            }

            // 3. Handle Grabbing/Dragging (Requires BOTH Alt and Left Click)
            bool clickPressed = Input.GetMouseButton(0);

            if (clickPressed)
            {
                if (!_isDragging)
                {
                    // Try to start dragging the hovered limb (if any)
                    if (hoveredLimb != null)
                    {
                        _grabbedLimb = hoveredLimb;
                        _grabOffset = mouseWorld - hoveredLimb.rb.position;
                        _isDragging = true;
                        _stretchSoundCooldown = 0f;
                        _lastMouseWorldPos = mouseWorld;
                        _lastLineTriggerTime = Time.time;

                        // If standing, temporarily simulate the entire limb group to prevent separation/detachment
                        if (hoveredLimb.body != null && hoveredLimb.body.standing)
                        {
                            _simulatedLimbGroup = GetLimbGroup(hoveredLimb, hoveredLimb.body);
                            if (_simulatedLimbGroup.Count > 0)
                            {
                                foreach (Limb limb in _simulatedLimbGroup)
                                {
                                    if (limb != null && limb.rb != null)
                                    {
                                        limb.rb.simulated = true;

                                        // Ignore collision with the main body to prevent catastrophic physical glitching/self-collision
                                        if (limb.body != null)
                                        {
                                            Collider2D limbCollider = limb.GetComponent<Collider2D>();
                                            if (limbCollider != null)
                                            {
                                                Collider2D[] bodyColliders = limb.body.GetComponents<Collider2D>();
                                                foreach (Collider2D bodyCol in bodyColliders)
                                                {
                                                    if (bodyCol != null)
                                                    {
                                                        Physics2D.IgnoreCollision(limbCollider, bodyCol, true);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                // Establish stable world-space physics anchors for the roots of the simulated chains
                                _activeJointAnchors.Clear();
                                foreach (Limb limb in _simulatedLimbGroup)
                                {
                                    if (limb != null && limb.joint != null && limb.joint.connectedBody != null)
                                    {
                                        Rigidbody2D conn = limb.joint.connectedBody;
                                        // If the connected body is NOT part of our simulated group, it is an external anchor!
                                        bool isConnInGroup = false;
                                        foreach (Limb groupLimb in _simulatedLimbGroup)
                                        {
                                            if (groupLimb != null && groupLimb.rb == conn)
                                            {
                                                isConnInGroup = true;
                                                break;
                                            }
                                        }

                                        if (!isConnInGroup)
                                        {
                                            _activeJointAnchors.Add(new JointAnchorState
                                            {
                                                joint = limb.joint,
                                                originalConnectedBody = conn,
                                                originalConnectedAnchor = limb.joint.connectedAnchor
                                            });

                                            // Disconnect from the unsimulated rigidbody and anchor directly to the world point
                                            limb.joint.autoConfigureConnectedAnchor = false;
                                            limb.joint.connectedBody = null;
                                            limb.joint.connectedAnchor = conn.transform.TransformPoint(limb.joint.connectedAnchor);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Update active drag/tug physics
                    UpdateActiveDrag(mouseWorld, activeBody);
                }
            }
            else
            {
                // Release physical drag if Left Click is released (but keep petting active if holding Alt!)
                ReleaseActiveDragOnly();
            }

            _lastMouseWorldPos = mouseWorld;
            return activelyPettingHealthyThisFrame;
        }

        // For backwards compatibility
        private bool UpdateHoverAndPet(Body body)
        {
            return UpdateHoverAndPet(out _, out _);
        }

        private void UpdateActiveDrag(Vector2 mouseWorld, Body body)
        {
            if (_grabbedLimb == null || _grabbedLimb.dismembered || !_grabbedLimb.gameObject.activeInHierarchy || _grabbedLimb.body != body)
            {
                ReleaseActiveDragOnly();
                return;
            }

            Vector2 limbPos = _grabbedLimb.rb.position;
            Vector2 targetPos = mouseWorld - _grabOffset;

            // Prevent flying cheats when ragdolled by clamping the target Y-coordinate near the ground
            if (body != null && !body.standing)
            {
                // Raycast from above the target's horizontal position down to find ground level
                float checkStartX = targetPos.x;
                float checkStartY = limbPos.y + 6f;
                RaycastHit2D hit = Physics2D.Raycast(new Vector2(checkStartX, checkStartY), Vector2.down, 18f, LayerMask.GetMask("Ground"));
                if (hit.collider != null)
                {
                    float groundY = hit.point.y;
                    float maxAllowedY = groundY + 1.1f; // Max height for dragged limbs
                    if (targetPos.y > maxAllowedY)
                    {
                        targetPos.y = maxAllowedY;
                    }
                }
                else
                {
                    // Fallback if no ground found under the target: clamp target Y to current limb Y to block pulling up
                    if (targetPos.y > limbPos.y)
                    {
                        targetPos.y = limbPos.y;
                    }
                }
            }

            Vector2 diff = targetPos - limbPos;
            float dist = diff.magnitude;

            float maxAllowedDistance = Plugin.Cfg.AutoReleaseDistance.Value;
            float releaseThreshold = maxAllowedDistance * 2.2f;

            if (dist > releaseThreshold)
            {
                // Play slipping/stretch sound on release
                Sound.Play("stretch", limbPos, volume: 0.35f, pitchShift: true);
                ReleaseActiveDragOnly();
                return;
            }

            // Apply physical spring force to "tug" the body part (only if simulated!)
            if (_grabbedLimb.rb.simulated)
            {
                float springK = Plugin.Cfg.PullStrength.Value;
                float damper = 9.5f; // Cushioned dampening for ultra soft touch
                Vector2 force = diff * springK - _grabbedLimb.rb.velocity * damper;

                // Limit vertical lifting force when ragdolled to prevent flying cheats
                if (body != null && !body.standing)
                {
                    float maxAllowedHeight = 1.0f; // Max height off the ground for ragdoll torso
                    RaycastHit2D hit = Physics2D.Raycast(body.transform.position, Vector2.down, 10f, LayerMask.GetMask("Ground"));
                    if (hit.collider != null)
                    {
                        float height = hit.distance;
                        if (height > maxAllowedHeight)
                        {
                            if (force.y > 0f)
                            {
                                // Blend the upward force down to zero based on how far we exceed the max height
                                float excess = height - maxAllowedHeight;
                                float scale = Mathf.Clamp01(1f - excess * 2.5f); // fully cuts off vertical force within 0.4f units of excess
                                force.y *= scale;
                            }
                        }
                    }
                    else
                    {
                        // If no ground detected at all beneath the body, disable upward force entirely
                        if (force.y > 0f) force.y = 0f;
                    }
                }

                _grabbedLimb.rb.AddForce(force * _grabbedLimb.rb.mass);
            }

            // Play tension stretching sound and apply minor stress/pain if pulled near limit
            if (dist > maxAllowedDistance * 0.65f)
            {
                _stretchSoundCooldown += Time.deltaTime;
                if (_stretchSoundCooldown >= 0.9f)
                {
                    _stretchSoundCooldown = 0f;
                    Sound.Play("stretch", limbPos, volume: 0.3f, pitchShift: true);
                }

                if (dist > maxAllowedDistance * 0.85f)
                {
                    // Gentle warning pain spike
                    _grabbedLimb.pain = Mathf.Min(100f, _grabbedLimb.pain + Time.deltaTime * 2.5f);
                    
                    if (_grabbedLimb.broken || _grabbedLimb.dislocated)
                    {
                        // Painful to pull a broken/dislocated limb
                        _grabbedLimb.pain = Mathf.Min(100f, _grabbedLimb.pain + Time.deltaTime * 12f);
                        if (Time.time - _lastLineTriggerTime > 1.8f)
                        {
                            _lastLineTriggerTime = Time.time;
                            TriggerPainReaction(_grabbedLimb);
                        }
                    }
                }
            }
        }

        private void TriggerHappyReaction(Limb limb)
        {
            if (limb.body == null) return;

            if (limb.body.conscious)
            {
                Sound.Play("exert" + UnityEngine.Random.Range(1, 5), limb.rb.position, volume: 0.12f, pitchShift: true, pitch: 1.35f);

                if (UnityEngine.Random.value < 0.35f)
                {
                    Sound.Play("dogshake", limb.rb.position, volume: 0.12f, pitchShift: true);
                }

                if (Plugin.Cfg.EnableHappyWhimpers.Value && limb.body.talker != null)
                {
                    string randomLine = HappyLines[UnityEngine.Random.Range(0, HappyLines.Count)];
                    limb.body.talker.Talk(randomLine, null, force: true, resetTalkTimer: true);
                }
            }
        }

        private void TriggerPainReaction(Limb limb)
        {
            if (limb.body == null) return;

            if (limb.body.conscious)
            {
                limb.body.Scream();
                Sound.Play("exert" + UnityEngine.Random.Range(1, 5), limb.rb.position, volume: 0.85f, pitchShift: true, pitch: 0.85f);
                
                if (UnityEngine.Random.value < 0.4f)
                {
                    Sound.Play("gore" + UnityEngine.Random.Range(1, 6), limb.rb.position, volume: 0.5f, pitchShift: true);
                }

                if (limb.body.talker != null)
                {
                    string randomLine = PainLines[UnityEngine.Random.Range(0, PainLines.Count)];
                    limb.body.talker.Talk(randomLine, limb, force: true, resetTalkTimer: true);
                }
            }
            else
            {
                limb.rb.AddForce(UnityEngine.Random.insideUnitCircle * 26f * limb.rb.mass, ForceMode2D.Impulse);
                if (limb.body.baseLimb != null)
                {
                    limb.body.baseLimb.rb.AddForce(UnityEngine.Random.insideUnitCircle * 15f * limb.body.baseLimb.rb.mass, ForceMode2D.Impulse);
                }

                Sound.Play("exert" + UnityEngine.Random.Range(1, 5), limb.rb.position, volume: 0.35f, pitchShift: true, pitch: 0.72f);
            }
        }

        private void ReleaseActiveDragOnly()
        {
            // Restore original connected bodies and anchors of our active joint anchors
            if (_activeJointAnchors != null && _activeJointAnchors.Count > 0)
            {
                foreach (JointAnchorState state in _activeJointAnchors)
                {
                    if (state.joint != null)
                    {
                        state.joint.connectedBody = state.originalConnectedBody;
                        state.joint.connectedAnchor = state.originalConnectedAnchor;
                    }
                }
                _activeJointAnchors.Clear();
            }

            if (_simulatedLimbGroup != null && _simulatedLimbGroup.Count > 0)
            {
                foreach (Limb limb in _simulatedLimbGroup)
                {
                    if (limb != null && limb.rb != null)
                    {
                        limb.rb.simulated = false;
                        limb.rb.velocity = Vector2.zero;
                        limb.rb.angularVelocity = 0f;
                    }
                }
                _simulatedLimbGroup.Clear();
            }
            if (_grabbedLimb != null && _grabbedLimbSimulated)
            {
                _grabbedLimb.rb.simulated = false;
                _grabbedLimb.rb.velocity = Vector2.zero;
                _grabbedLimb.rb.angularVelocity = 0f;
            }
            _grabbedLimb = null;
            _isDragging = false;
            _grabbedLimbSimulated = false;
        }

        private void ReleaseGrab()
        {
            ReleaseActiveDragOnly();
            _happyPetTimer = 0f;
            _painPetTimer = 0f;
            _lastActivePetTime = 0f;
            _lastActivePetTimePerBody.Clear();
        }

        public void UpdateJointAnchors()
        {
            if (_activeJointAnchors != null && _activeJointAnchors.Count > 0)
            {
                foreach (JointAnchorState state in _activeJointAnchors)
                {
                    if (state.joint != null && state.originalConnectedBody != null)
                    {
                        // Sync world anchor with animated parent's current visual position
                        state.joint.connectedAnchor = state.originalConnectedBody.transform.TransformPoint(state.originalConnectedAnchor);
                    }
                }
            }
        }

        private List<Limb> GetLimbGroup(Limb grabbed, Body body)
        {
            List<Limb> group = new List<Limb>();
            if (body == null || body.limbs == null || grabbed == null) return group;
            int index = Array.IndexOf(body.limbs, grabbed);
            if (index < 0) return group;

            if (grabbed.isArm)
            {
                // Determine if Front Arm (3, 4, 5) or Back Arm (6, 7, 8)
                int start = (index >= 3 && index <= 5) ? 3 : 6;
                for (int i = start; i < start + 3; i++)
                {
                    if (i < body.limbs.Length && body.limbs[i] != null && !body.limbs[i].dismembered)
                    {
                        group.Add(body.limbs[i]);
                    }
                }
            }
            else if (grabbed.isLegLimb)
            {
                // Determine if Front Leg (9, 10, 11) or Back Leg (12, 13, 14)
                int start = (index >= 9 && index <= 11) ? 9 : 12;
                for (int i = start; i < start + 3; i++)
                {
                    if (i < body.limbs.Length && body.limbs[i] != null && !body.limbs[i].dismembered)
                    {
                        group.Add(body.limbs[i]);
                    }
                }
            }
            else
            {
                // Fallback for non-vital extremity if it's not strictly categorized as arm/leg but is tuggable
                bool isTuggableLimb = !grabbed.isVital && !grabbed.isHead && !grabbed.isAbdomen;
                if (isTuggableLimb)
                {
                    group.Add(grabbed);
                }
            }
            return group;
        }
    }
}
