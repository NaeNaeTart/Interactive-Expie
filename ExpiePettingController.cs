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
        private bool _tempSimulated;

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
                RestoreLimbSimulation(null);
                return;
            }

            bool modifierPressed = Plugin.Cfg.ModifierKey.Value == KeyCode.None || Input.GetKey(Plugin.Cfg.ModifierKey.Value);
            bool mousePressed = Input.GetMouseButton(0);

            // Handle temporary limb simulation to allow dragging while standing
            if (modifierPressed && activeBody.standing)
            {
                EnsureLimbSimulation(activeBody);
            }
            else if (!modifierPressed || !activeBody.standing)
            {
                RestoreLimbSimulation(activeBody);
            }

            if (modifierPressed && mousePressed)
            {
                if (!_isDragging)
                {
                    TryGrabLimb();
                }
                else
                {
                    UpdateDragAndPet(activeBody);
                }
            }
            else
            {
                ReleaseGrab();
            }
        }

        private Body? FindActiveBody()
        {
            return FindObjectOfType<Body>();
        }

        private void EnsureLimbSimulation(Body body)
        {
            if (!_tempSimulated)
            {
                _tempSimulated = true;
                foreach (Limb limb in body.limbs)
                {
                    if (limb != null && !limb.dismembered)
                    {
                        limb.rb.simulated = true;
                    }
                }
            }
        }

        private void RestoreLimbSimulation(Body? body)
        {
            if (_tempSimulated)
            {
                _tempSimulated = false;
                if (body != null && body.standing)
                {
                    foreach (Limb limb in body.limbs)
                    {
                        if (limb != null && !limb.dismembered)
                        {
                            limb.rb.simulated = false;
                            limb.rb.velocity = Vector2.zero;
                            limb.rb.angularVelocity = 0f;
                        }
                    }
                }
            }
        }

        private void TryGrabLimb()
        {
            if (Camera.main == null) return;

            Vector3 mouseWorld3D = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mouseWorld = new Vector2(mouseWorld3D.x, mouseWorld3D.y);

            // Perform 2D raycast to detect limb colliders
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
                    _grabbedLimb = limb;
                    _grabOffset = mouseWorld - limb.rb.position;
                    _isDragging = true;
                    _stretchSoundCooldown = 0f;
                    _happyPetTimer = 0f;
                    _painPetTimer = 0f;
                    _lastMouseWorldPos = mouseWorld;
                    _lastLineTriggerTime = Time.time;
                }
            }
        }

        private void UpdateDragAndPet(Body body)
        {
            if (_grabbedLimb == null || _grabbedLimb.dismembered || !_grabbedLimb.gameObject.activeInHierarchy || _grabbedLimb.body != body)
            {
                ReleaseGrab();
                return;
            }

            if (Camera.main == null) return;

            Vector3 mouseWorld3D = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mouseWorld = new Vector2(mouseWorld3D.x, mouseWorld3D.y);

            Vector2 limbPos = _grabbedLimb.rb.position;
            Vector2 targetPos = mouseWorld - _grabOffset;
            Vector2 diff = targetPos - limbPos;
            float dist = diff.magnitude;

            // ⚠️ Gentle Constraint: Automatically release if pulled too hard (grip slips!)
            float maxAllowedDistance = Plugin.Cfg.AutoReleaseDistance.Value;
            if (dist > maxAllowedDistance)
            {
                // Play slipping/stretch sound on release
                Sound.Play("stretch", limbPos, volume: 0.35f, pitchShift: true);
                ReleaseGrab();
                return;
            }

            // 1. Apply physical spring force to "tug" the body part
            float springK = Plugin.Cfg.PullStrength.Value;
            float damper = 9.5f; // Cushioned dampening for ultra soft touch
            Vector2 force = diff * springK - _grabbedLimb.rb.velocity * damper;
            _grabbedLimb.rb.AddForce(force * _grabbedLimb.rb.mass);

            // 2. Play tension stretching sound and apply minor stress/pain if pulled near limit
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

            // 3. Petting detection based on cursor movement back and forth
            float mouseDelta = Vector2.Distance(_lastMouseWorldPos, mouseWorld);
            float mouseSpeed = mouseDelta / Time.deltaTime;

            if (mouseSpeed > 0.35f && mouseSpeed < 14f && dist < maxAllowedDistance * 0.7f)
            {
                // Trigger a tiny micro-movement tremor to show physical touch feedback
                _grabbedLimb.rb.AddForce(UnityEngine.Random.insideUnitCircle * 5f * _grabbedLimb.rb.mass);

                if (_grabbedLimb.isHead)
                {
                    // Nuzzle up/lift head slightly towards the hand
                    _grabbedLimb.rb.AddForce(Vector2.up * 6.5f * _grabbedLimb.rb.mass);
                }

                // Check skin damage
                if (_grabbedLimb.skinHealth >= 95f)
                {
                    // Case A: Healthy Petting!
                    _grabbedLimb.pain = Mathf.Max(0f, _grabbedLimb.pain - Time.deltaTime * 7.5f);
                    body.happiness = Mathf.Min(100f, body.happiness + Time.deltaTime * Plugin.Cfg.HappinessGainRate.Value);
                    body.traumaAmount = Mathf.Max(0f, body.traumaAmount - Time.deltaTime * Plugin.Cfg.StressDecreaseRate.Value);

                    _happyPetTimer += Time.deltaTime;
                    if (_happyPetTimer >= UnityEngine.Random.Range(2.0f, 3.5f))
                    {
                        _happyPetTimer = 0f;
                        TriggerHappyReaction(_grabbedLimb);
                    }
                }
                else
                {
                    // Case B: Painful touch on skin wounds/burns!
                    _grabbedLimb.pain = Mathf.Min(100f, _grabbedLimb.pain + Time.deltaTime * Plugin.Cfg.PainIncreaseRate.Value);
                    body.shock = Mathf.Min(100f, body.shock + Time.deltaTime * 7.5f);
                    body.happiness = Mathf.Max(-100f, body.happiness - Time.deltaTime * 12f);

                    _painPetTimer += Time.deltaTime;
                    if (_painPetTimer >= UnityEngine.Random.Range(0.8f, 1.5f))
                    {
                        _painPetTimer = 0f;
                        TriggerPainReaction(_grabbedLimb);
                    }
                }
            }

            _lastMouseWorldPos = mouseWorld;
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

        private void ReleaseGrab()
        {
            _grabbedLimb = null;
            _isDragging = false;
            _happyPetTimer = 0f;
            _painPetTimer = 0f;
        }
    }
}
