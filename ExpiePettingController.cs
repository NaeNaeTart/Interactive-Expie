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
        private float _stretchSoundCooldown;
        private float _happyPetTimer;
        private float _painPetTimer;
        private float _lastLineTriggerTime;
        private bool _grabbedLimbSimulated;
        private float _lastActivePetTime;
        private bool _isPettingHealthy;
        private List<Limb> _simulatedLimbGroup = new List<Limb>();

        public bool IsPettingRecently => (Time.time - _lastActivePetTime) < 1.5f;
        public bool IsPettingHealthy => _isPettingHealthy;

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

        private void Update()
        {
            Body? activeBody = FindActiveBody();
            if (activeBody == null)
            {
                ReleaseGrab();
                return;
            }

            bool altPressed = Plugin.Cfg.ModifierKey.Value == KeyCode.None ? Input.GetMouseButton(0) : Input.GetKey(Plugin.Cfg.ModifierKey.Value);

            if (altPressed)
            {
                // Active petting and hovered limb detection
                UpdateHoverAndPet(activeBody);
            }
            else
            {
                ReleaseGrab();
            }
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

        private Limb? DetectLimbAt(Vector2 mouseWorld, Body body)
        {
            // First attempt: Physics Raycast (works when limbs are simulated / ragdoll)
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

            // Second attempt (Fallback): SpriteRenderer 2D Bounds Check (essential when standing up and simulated = false)
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
                return closestLimb;
            }

            return null;
        }

        private void UpdateHoverAndPet(Body body)
        {
            if (Camera.main == null) return;

            Vector3 mouseWorld3D = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mouseWorld = new Vector2(mouseWorld3D.x, mouseWorld3D.y);

            // 1. Detect hovered limb (robust check supporting non-simulated standing limbs)
            Limb? hoveredLimb = DetectLimbAt(mouseWorld, body);

            // 2. Handle Petting (Requires only Alt, doesn't require Click)
            if (hoveredLimb != null)
            {
                bool wasPettingRecently = IsPettingRecently;
                _lastActivePetTime = Time.time;
                _isPettingHealthy = (hoveredLimb.skinHealth >= 95f);

                if (!wasPettingRecently)
                {
                    // Instant physical feedback upon starting petting
                    if (_isPettingHealthy)
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
                    hoveredLimb.pain = Mathf.Max(0f, hoveredLimb.pain - Time.deltaTime * 7.5f);
                    body.happiness = Mathf.Min(100f, body.happiness + Time.deltaTime * Plugin.Cfg.HappinessGainRate.Value);
                    body.traumaAmount = Mathf.Max(0f, body.traumaAmount - Time.deltaTime * Plugin.Cfg.StressDecreaseRate.Value);

                    _happyPetTimer += Time.deltaTime;
                    if (_happyPetTimer >= UnityEngine.Random.Range(2.0f, 3.5f))
                    {
                        _happyPetTimer = 0f;
                        TriggerHappyReaction(hoveredLimb);
                    }
                }
                else
                {
                    // Case B: Painful touch on skin wounds/burns!
                    hoveredLimb.pain = Mathf.Min(100f, hoveredLimb.pain + Time.deltaTime * Plugin.Cfg.PainIncreaseRate.Value);
                    body.shock = Mathf.Min(100f, body.shock + Time.deltaTime * 7.5f);
                    body.happiness = Mathf.Max(-100f, body.happiness - Time.deltaTime * 12f);

                    _painPetTimer += Time.deltaTime;
                    if (_painPetTimer >= UnityEngine.Random.Range(0.8f, 1.5f))
                    {
                        _painPetTimer = 0f;
                        TriggerPainReaction(hoveredLimb);
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
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Update active drag/tug physics
                    UpdateActiveDrag(mouseWorld, body);
                }
            }
            else
            {
                // Release physical drag if Left Click is released (but keep petting active if holding Alt!)
                ReleaseActiveDragOnly();
            }

            _lastMouseWorldPos = mouseWorld;
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
