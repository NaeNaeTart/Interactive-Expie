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

            bool modifierPressed = Plugin.Cfg.ModifierKey.Value == KeyCode.None || Input.GetKey(Plugin.Cfg.ModifierKey.Value);
            bool mousePressed = Input.GetMouseButton(0);

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
            // Find the active subject in the scene
            return FindObjectOfType<Body>();
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

            // 1. Apply physical spring force to "tug" the body part
            float springK = Plugin.Cfg.PullStrength.Value;
            float damper = 11f;
            Vector2 force = diff * springK - _grabbedLimb.rb.velocity * damper;
            _grabbedLimb.rb.AddForce(force * _grabbedLimb.rb.mass);

            // 2. Play tension stretching sound and apply minor stress/pain if pulled hard
            if (dist > 1.3f)
            {
                _stretchSoundCooldown += Time.deltaTime;
                if (_stretchSoundCooldown >= 0.8f)
                {
                    _stretchSoundCooldown = 0f;
                    Sound.Play("stretch", limbPos, volume: 0.45f, pitchShift: true);
                }

                if (dist > 2.2f)
                {
                    // Gentle warning pain spike
                    _grabbedLimb.pain = Mathf.Min(100f, _grabbedLimb.pain + Time.deltaTime * 3.5f);
                    
                    if (_grabbedLimb.broken || _grabbedLimb.dislocated)
                    {
                        // Extremely painful to pull a broken/dislocated limb
                        _grabbedLimb.pain = Mathf.Min(100f, _grabbedLimb.pain + Time.deltaTime * 18f);
                        if (Time.time - _lastLineTriggerTime > 1.5f)
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

            // A pleasing petting velocity is between 0.3 and 14 units/sec, and cursor must be near the limb center
            if (mouseSpeed > 0.35f && mouseSpeed < 14f && dist < 1.3f)
            {
                // Trigger a tiny micro-movement tremor to show physical touch feedback
                _grabbedLimb.rb.AddForce(UnityEngine.Random.insideUnitCircle * 6.5f * _grabbedLimb.rb.mass);

                if (_grabbedLimb.isHead)
                {
                    // Nuzzle up/lift head slightly towards the hand
                    _grabbedLimb.rb.AddForce(Vector2.up * 8f * _grabbedLimb.rb.mass);
                }

                // Check skin damage
                if (_grabbedLimb.skinHealth >= 95f)
                {
                    // Case A: Healthy Petting!
                    // Slowly soothe pain and increase happiness
                    _grabbedLimb.pain = Mathf.Max(0f, _grabbedLimb.pain - Time.deltaTime * 6.5f);
                    body.happiness = Mathf.Min(100f, body.happiness + Time.deltaTime * Plugin.Cfg.HappinessGainRate.Value);
                    body.traumaAmount = Mathf.Max(0f, body.traumaAmount - Time.deltaTime * Plugin.Cfg.StressDecreaseRate.Value);

                    _happyPetTimer += Time.deltaTime;
                    if (_happyPetTimer >= UnityEngine.Random.Range(2.2f, 3.8f))
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
                    if (_painPetTimer >= UnityEngine.Random.Range(0.8f, 1.6f))
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
                // Play a cute soft vocal sigh/coo (exert sounds with raised pitch)
                Sound.Play("exert" + UnityEngine.Random.Range(1, 5), limb.rb.position, volume: 0.12f, pitchShift: true, pitch: 1.35f);

                if (UnityEngine.Random.value < 0.35f)
                {
                    Sound.Play("dogshake", limb.rb.position, volume: 0.15f, pitchShift: true);
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
                // Play screaming and painful sounds
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
                // Unconscious spasm & physical groan when touched on wounds
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
