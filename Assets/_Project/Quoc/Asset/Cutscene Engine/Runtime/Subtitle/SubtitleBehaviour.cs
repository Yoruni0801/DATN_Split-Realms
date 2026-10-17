using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM && INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CutsceneEngine
{
    [Serializable]
    public class SubtitleBehaviour : PlayableBehaviour
    {
        public SubtitleClip clip;
        bool _initialized;
        double _timePerChar;
        double _desiredDuration;
        SubtitleText _subtitle;
        TypingEffectParameter _typingEffectParameter;
        PlayableDirector _director;
        bool _fullyRevealed;
        double _lastLocalTime;
        double _lastDuration;

        internal bool IsActiveTypingSubtitle =>
            _initialized
            && clip != null
            && !clip.isVirtual
            && _subtitle
            && _subtitle.textDisplayEffect == TextDisplayEffect.Typing;

        internal void SetDirector(PlayableDirector director)
        {
            _director = director;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var subtitle = playerData as SubtitleText;
            _subtitle = subtitle;
            _lastLocalTime = playable.GetTime();
            _lastDuration = playable.GetDuration();

            if (!_initialized)
            {
                clip.EnterCallback(this, subtitle);
                if(subtitle)
                {
                    switch (subtitle.textDisplayEffect)
                    {
                        case TextDisplayEffect.None:
                            if (!clip.isVirtual) subtitle.SetRawText(clip.GetText());
                            break;
                        case TextDisplayEffect.Fade:
                            if (!clip.isVirtual) subtitle.SetRawText(clip.GetText());
                            break;
                        case TextDisplayEffect.Typing:
                            InitializeTypingEffect(subtitle, in playable);
                            if (!clip.isVirtual)
                            {
                                SubtitleSkipInput.Register(this, _director);
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                _initialized = true;
            }

            if (SubtitleSkipInput.WasSkipPressedThisFrame())
            {
                SubtitleSkipInput.TryHandleSkipPress();
            }

            if(!clip.isVirtual)
            {
                if(subtitle)
                {
                    if (subtitle.textDisplayEffect == TextDisplayEffect.Typing && !_fullyRevealed)
                        UpdateTextInTypingEffect(subtitle, playable.GetTime() / _desiredDuration);
                }
            }
            UpdateAlpha(subtitle, in playable);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (info.effectiveWeight <= 0)
            {
                SubtitleSkipInput.Unregister(this);
                _initialized = false;
                _fullyRevealed = false;
                if(_subtitle) _subtitle.Clear(true);
                
                clip.ExitCallback(this, _subtitle);
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            SubtitleSkipInput.Unregister(this);
            _initialized = false;
            _fullyRevealed = false;
        }

        public bool RevealAll()
        {
            if (!IsActiveTypingSubtitle || _fullyRevealed)
            {
                return false;
            }

            _fullyRevealed = true;
            _subtitle.SetRawText(clip.GetText());
            return true;
        }

        public bool SkipToEnd()
        {
            if (!IsActiveTypingSubtitle || !_fullyRevealed || _director == null)
            {
                return false;
            }

            double remainingTime = _lastDuration - _lastLocalTime;
            if (double.IsNaN(remainingTime) || double.IsInfinity(remainingTime))
            {
                return false;
            }

            bool wasPlaying = _director.state == PlayState.Playing;
            if (wasPlaying)
            {
                _director.Pause();
            }

            SubtitleTimelineSeekBridge.BeginExplicitSeek(_director);
            try
            {
                _director.time = Math.Min(_director.duration, _director.time + Math.Max(remainingTime, 0.05d));
                _director.Evaluate();
            }
            finally
            {
                SubtitleTimelineSeekBridge.EndExplicitSeek(_director);
            }

            if (wasPlaying)
            {
                _director.Play();
            }

            return true;
        }

        void InitializeTypingEffect(SubtitleText subtitle, in Playable playable)
        {
            _typingEffectParameter = clip.overrideTypingEffectParameter ? 
                clip.typingEffectParameter : subtitle.typingEffectParameter;
            
            var _normalizedText = clip.GetText().Normalize(NormalizationForm.FormD);
            var _normalizedTextLength = _normalizedText.Length;

            var param = _typingEffectParameter;
                
            _timePerChar = param.timeMethod == TypingEffectParameter.TimeMethod.PerChar
                ? param.timePerChar
                : param.totalDuration / _normalizedTextLength;

            var duration = playable.GetDuration();

            _desiredDuration = param.timeMethod == TypingEffectParameter.TimeMethod.PerChar
                ? param.timePerChar * _normalizedTextLength
                : param.totalDuration;

            foreach (var c in _normalizedText)
            {
                if (param.HasAdditionalCharSetting(c, out var time))
                {
                    _desiredDuration += time - _timePerChar;
                }
            }

            if (_desiredDuration > duration - param.fadeOutTime)
            {
                _desiredDuration = duration - param.fadeOutTime;
            }
        }

        void UpdateTextInTypingEffect(SubtitleText subtitle, double normalizedTime)
        {
            var displayText = clip.GetText();
            if (normalizedTime < 0 || string.IsNullOrWhiteSpace(displayText))
            {
                subtitle.SetRawText(displayText);
                return;
            }

            if (normalizedTime >= 1)
            {
                subtitle.SetRawText(displayText);
                return;
            }

            var elapsed = 0d;
            foreach (var str in TypingEffect.GetTypingText(displayText))
            {
                if(_typingEffectParameter.HasAdditionalCharSetting(str[^1], out var time)) elapsed += time;
                else elapsed += _timePerChar;
                if (elapsed / _desiredDuration < normalizedTime) continue;
                subtitle.SetRawText(str);
                return;
            }
        }

        void UpdateAlpha(SubtitleText subtitle, in Playable playable)
        {
            if(!subtitle) return;
            var time = playable.GetTime();
            var left = playable.GetDuration() - time;
            
            switch (subtitle.textDisplayEffect)
            {
                case TextDisplayEffect.None:
                    subtitle.SetTextAlpha(1);
                    break;
                case TextDisplayEffect.Fade:
                    if (left < subtitle.subtitleFadeParameter.fadeOutTime)
                    {
                        subtitle.SetTextAlpha((float)left / subtitle.subtitleFadeParameter.fadeOutTime);
                    }
                    else if (time < subtitle.subtitleFadeParameter.fadeInTime)
                    {
                        subtitle.SetTextAlpha((float)time / subtitle.subtitleFadeParameter.fadeInTime);
                    }
                    else
                    {
                        subtitle.SetTextAlpha(1);
                    }
                    break;
                case TextDisplayEffect.Typing:
                    if (left < _typingEffectParameter.fadeOutTime)
                    {
                        subtitle.SetTextAlpha((float)left / _typingEffectParameter.fadeOutTime);
                    }
                    else
                    {
                        subtitle.SetTextAlpha(1);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public static class SubtitleSkipInput
    {
        readonly struct ActiveSubtitle
        {
            public readonly SubtitleBehaviour behaviour;
            public readonly PlayableDirector director;

            public ActiveSubtitle(SubtitleBehaviour behaviour, PlayableDirector director)
            {
                this.behaviour = behaviour;
                this.director = director;
            }
        }

        static readonly List<ActiveSubtitle> ActiveSubtitles = new List<ActiveSubtitle>();
        static bool _reserveUntilSkipReleased;
        static int _lastConsumedSpaceFrame = -1;
        static float _lastSkipTime = -100f;
        // static float _lastSkipTime = -100f;

        public static bool IsSkipReserved
        {
            get
            {
                RefreshSkipReleaseState();

                if (_reserveUntilSkipReleased)
                {
                    return true;
                }

                if (Time.time - _lastSkipTime < 0.05f)
                {
                    return true;
                }

                for (int i = ActiveSubtitles.Count - 1; i >= 0; i--)
                {
                    ActiveSubtitle activeSubtitle = ActiveSubtitles[i];
                    if (activeSubtitle.director != null
                        && activeSubtitle.director.state == PlayState.Playing
                        && activeSubtitle.behaviour.IsActiveTypingSubtitle)
                    {
                        if (IsSkipHeld())
                        {
                            _reserveUntilSkipReleased = true;
                        }

                        return true;
                    }
                }

                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            ActiveSubtitles.Clear();
            _reserveUntilSkipReleased = false;
            _lastConsumedSpaceFrame = -1;
        }

        public static bool HasActiveSubtitle(PlayableDirector director)
        {
            if (director == null || director.state != PlayState.Playing)
            {
                return false;
            }

            for (int i = ActiveSubtitles.Count - 1; i >= 0; i--)
            {
                ActiveSubtitle activeSubtitle = ActiveSubtitles[i];
                if (activeSubtitle.director == director
                    && activeSubtitle.behaviour.IsActiveTypingSubtitle)
                {
                    return true;
                }
            }

            return false;
        }

        internal static void Register(SubtitleBehaviour behaviour, PlayableDirector director)
        {
            if (behaviour == null || director == null)
            {
                return;
            }

            Unregister(behaviour);
            ActiveSubtitles.Add(new ActiveSubtitle(behaviour, director));
        }

        internal static void Unregister(SubtitleBehaviour behaviour)
        {
            for (int i = ActiveSubtitles.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(ActiveSubtitles[i].behaviour, behaviour))
                {
                    ActiveSubtitles.RemoveAt(i);
                }
            }
        }

        internal static bool TryRevealActiveSubtitle(PlayableDirector director)
        {
            if (director == null || director.state != PlayState.Playing)
            {
                return false;
            }

            if (Time.time - _lastSkipTime < 0.05f)
            {
                return false;
            }

            bool foundActiveSubtitle = false;
            bool revealedSubtitle = false;

            for (int i = ActiveSubtitles.Count - 1; i >= 0; i--)
            {
                ActiveSubtitle activeSubtitle = ActiveSubtitles[i];
                if (activeSubtitle.director != director
                    || !activeSubtitle.behaviour.IsActiveTypingSubtitle)
                {
                    continue;
                }

                foundActiveSubtitle = true;
                revealedSubtitle |= activeSubtitle.behaviour.RevealAll();
            }

            if (foundActiveSubtitle && !revealedSubtitle)
            {
                for (int i = ActiveSubtitles.Count - 1; i >= 0; i--)
                {
                    ActiveSubtitle activeSubtitle = ActiveSubtitles[i];
                    if (activeSubtitle.director == director
                        && activeSubtitle.behaviour.SkipToEnd())
                    {
                        break;
                    }
                }
            }

            if (foundActiveSubtitle)
            {
                _reserveUntilSkipReleased = revealedSubtitle;
                _lastSkipTime = Time.time;
            }

            return foundActiveSubtitle;
        }

        public static bool TryHandleSkipPress()
        {
            if (_lastConsumedSpaceFrame == Time.frameCount)
            {
                return false;
            }

            bool handled = TryHandleSkipPressInternal();
            if (handled)
            {
                _lastConsumedSpaceFrame = Time.frameCount;
            }

            return handled;
        }

        static bool TryHandleSkipPressInternal()
        {
            for (int i = ActiveSubtitles.Count - 1; i >= 0; i--)
            {
                ActiveSubtitle activeSubtitle = ActiveSubtitles[i];
                if (activeSubtitle.director != null
                    && activeSubtitle.director.state == PlayState.Playing
                    && activeSubtitle.behaviour.IsActiveTypingSubtitle)
                {
                    return TryRevealActiveSubtitle(activeSubtitle.director);
                }
            }

            return false;
        }

        internal static bool WasSkipPressedThisFrame()
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
#if ENABLE_INPUT_SYSTEM && INPUT_SYSTEM
            pressed |= Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#endif
            return pressed;
        }

        internal static void RefreshSkipReleaseState()
        {
            if (_reserveUntilSkipReleased && !IsSkipHeld())
            {
                _reserveUntilSkipReleased = false;
            }
        }

        static bool IsSkipHeld()
        {
            bool held = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            held |= Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
#endif
#if ENABLE_INPUT_SYSTEM && INPUT_SYSTEM
            held |= Keyboard.current != null
                && (Keyboard.current.enterKey.isPressed
                    || Keyboard.current.numpadEnterKey.isPressed);
#endif
            return held;
        }
    }

    static class SubtitleTimelineSeekBridge
    {
        static Type seekContextType;
        static System.Reflection.MethodInfo beginExplicitSeek;
        static System.Reflection.MethodInfo endExplicitSeek;

        public static void BeginExplicitSeek(PlayableDirector director)
        {
            ResolveMethods();
            beginExplicitSeek?.Invoke(null, new object[] { director });
        }

        public static void EndExplicitSeek(PlayableDirector director)
        {
            ResolveMethods();
            endExplicitSeek?.Invoke(null, new object[] { director });
        }

        static void ResolveMethods()
        {
            if (beginExplicitSeek != null && endExplicitSeek != null)
            {
                return;
            }

            seekContextType = ResolveSeekContextType();
            if (seekContextType == null)
            {
                return;
            }

            beginExplicitSeek = seekContextType.GetMethod(
                "BeginExplicitSeek",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            endExplicitSeek = seekContextType.GetMethod(
                "EndExplicitSeek",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        }

        static Type ResolveSeekContextType()
        {
            Type type = Type.GetType("TimelineSeekContext");
            if (type != null)
            {
                return type;
            }

            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType("TimelineSeekContext");
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}

