using UnityEngine;
using Duy.CameraSystem;

namespace Bao.Bosses.Ashwing
{
    /// <summary>Rig-aware presentation with bounded reusable feather and ribbon pools.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class AshwingBossVFX : MonoBehaviour
    {
        [Header("Authored assets")]
        [SerializeField] private Material plumageMaterial;
        [SerializeField] private Material filamentMaterial;
        [SerializeField] private Sprite featherSprite;
        [SerializeField] private SpriteRenderer[] plumage;
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform[] wingBones;
        [SerializeField] private SpriteRenderer[] wingSprites;
        [Header("Audio")]
        [SerializeField] private AudioClip callClip, chargeClip, featherClip, dashClip, parryClip, phaseClip, impactClip;
        [SerializeField, Range(0,1)] private float volume = .55f;
        [Header("Presentation")]
        [SerializeField, Range(0,1)] private float shakeStrength = .25f;
        [SerializeField, Range(.2f,2f)] private float effectsScale = 1.5f;
        public float EffectsScale => effectsScale;
        public Material Filament => filamentMaterial;
        public Sprite FeatherSprite => featherSprite;

        public static readonly Color Gold = new Color(1f,.64f,.18f,1);
        public static readonly Color Cinder = new Color(1f,.18f,.025f,1);
        public static readonly Color Hot = new Color(1f,.91f,.60f,1);
        private const int StrokeCount = 56, MoteCount = 112;
        private readonly Stroke[] strokes = new Stroke[StrokeCount];
        private readonly Mote[] motes = new Mote[MoteCount];
        private readonly Stroke[] telegraph = new Stroke[3];
        private readonly Stroke[] corona = new Stroke[3];
        private readonly TrailRenderer[] wingTrails = new TrailRenderer[4];
        private readonly SpriteRenderer[] chargeFeathers = new SpriteRenderer[3];
        private Transform effectRoot;
        private AshwingBossCombat combat;
        private AudioSource voice, chargeVoice;
        private MaterialPropertyBlock block;
        private float heat, flash, deathAge = -1, ambientTimer, dashTimer, hitSoundTime;
        private float phaseBurstAt = -1;
        private Quaternion rigBaseRotation;
        private Vector3 rigBasePosition;
        private Quaternion[] wingBaseRotation;
        private bool poseApplied;
        private float poseAngle, wingAngle;
        private static readonly int HeatId=Shader.PropertyToID("_Heat"), EnrageId=Shader.PropertyToID("_Enrage"),
            FlashId=Shader.PropertyToID("_HitFlash"), DissolveId=Shader.PropertyToID("_Dissolve");

        public sealed class Stroke
        {
            public LineRenderer Renderer;
            internal readonly Vector3[] Points = new Vector3[65];
            internal readonly Vector3[] Original = new Vector3[65];
            internal int Count;
            internal bool Used, Persistent;
            internal float Age, Duration, Expansion, Spin, Width;
            internal Color Color;
            internal Vector3 Center;
        }
        private sealed class Mote
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public Color Color;
            public float Age, Life, Scale, Spin;
        }

        private void Awake()
        {
            combat=GetComponent<AshwingBossCombat>();
            block=new MaterialPropertyBlock();
            if(plumage!=null) foreach(var sr in plumage) if(sr!=null && plumageMaterial!=null) sr.sharedMaterial=plumageMaterial;
            var root=new GameObject("Ashwing • pooled cinders");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            effectRoot=root.transform;
            for(int i=0;i<StrokeCount;i++)
            {
                var go=new GameObject("Filament");go.transform.SetParent(effectRoot,false);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=filamentMaterial;
                line.useWorldSpace=true;line.textureMode=LineTextureMode.Stretch;line.numCapVertices=3;
                line.numCornerVertices=2;line.sortingOrder=25;line.enabled=false;
                strokes[i]=new Stroke{Renderer=line};
            }
            for(int i=0;i<MoteCount;i++)
            {
                var go=new GameObject("Cinder feather");go.transform.SetParent(effectRoot,false);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=featherSprite;
                sr.sortingOrder=26;sr.enabled=false;
                motes[i]=new Mote{Renderer=sr};
            }
            for(int i=0;i<3;i++) { corona[i]=Acquire(); if(corona[i]!=null)corona[i].Renderer.sortingOrder=7; }
            for(int i=0;i<wingTrails.Length;i++)
            {
                var go=new GameObject("Wingtip heat ribbon");go.transform.SetParent(effectRoot,false);
                var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=filamentMaterial;
                trail.time=.26f;trail.startWidth=.105f*effectsScale;trail.endWidth=0;trail.minVertexDistance=.04f;
                trail.numCapVertices=3;trail.sortingOrder=16;trail.emitting=false;
                trail.startColor=new Color(1,.60f,.16f,.72f);trail.endColor=new Color(.7f,.045f,.01f,0);
                wingTrails[i]=trail;
            }
            for(int i=0;i<chargeFeathers.Length;i++)
            {
                var go=new GameObject("Charged feather glyph");go.transform.SetParent(effectRoot,false);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=featherSprite;sr.color=Hot;sr.sortingOrder=28;sr.enabled=false;
                chargeFeathers[i]=sr;
            }
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=.15f;voice.maxDistance=35;
            chargeVoice=gameObject.AddComponent<AudioSource>();chargeVoice.playOnAwake=false;chargeVoice.spatialBlend=.15f;
            wingBaseRotation=new Quaternion[wingBones==null?0:wingBones.Length];
        }

        private void Update()
        {
            RestorePose();
            float dt=Time.deltaTime;
            if(combat==null)return;
            float targetHeat=.12f+(combat.Enraged?.23f:0)+combat.Charge*.63f;
            if(combat.Beat==AshwingBeat.Dash)targetHeat=1;
            heat=Mathf.Lerp(heat,targetHeat,1-Mathf.Exp(-dt*10));
            flash=Mathf.MoveTowards(flash,0,dt*7);
            if(deathAge>=0) deathAge+=dt;
            if(phaseBurstAt>=0 && Time.time>=phaseBurstAt)
            {
                phaseBurstAt=-1;
                RingBurst(combat.BodyCenter,3.1f,.85f,Gold,3);
                Burst(combat.BodyCenter,36,4.6f);
                Shake(.32f,1);
            }
            UpdatePools(dt);
            if(deathAge<0)
            {
                UpdateCorona();
                ambientTimer-=dt;
                if(ambientTimer<=0)
                {
                    ambientTimer=combat.IsEngaged?.10f:.32f;
                    EmitWingFeathers(combat.Enraged?2:1);
                }
                if(combat.Beat==AshwingBeat.Dash)
                {
                    dashTimer-=dt;
                    if(dashTimer<=0){dashTimer=.055f;WingSlash(combat.Aim);}
                }
            }
        }

        private void LateUpdate()
        {
            if(combat==null)return;
            if(plumage!=null)foreach(var sr in plumage)
            {
                if(sr==null)continue;
                sr.GetPropertyBlock(block);
                block.SetFloat(HeatId,heat);block.SetFloat(EnrageId,combat.Enraged?1:0);
                block.SetFloat(FlashId,flash);block.SetFloat(DissolveId,deathAge<0?0:Mathf.Clamp01(deathAge/1.45f));
                sr.SetPropertyBlock(block);
            }
            if(rigRoot==null)return;
            float desiredTilt=0,desiredWing=0;
            switch(combat.Beat)
            {
                case AshwingBeat.Windup:
                    desiredTilt=combat.Skill==AshwingSkill.HuntingDash?-12:combat.Skill==AshwingSkill.CinderRazor?-5:3;
                    desiredWing=combat.Skill==AshwingSkill.HuntingDash?22:combat.Skill==AshwingSkill.CinderRazor?9:-14;
                    desiredWing*=combat.Charge;break;
                case AshwingBeat.Dash: desiredTilt=-17;desiredWing=28;break;
                case AshwingBeat.Release: desiredTilt=6;desiredWing=-16;break;
                case AshwingBeat.Stagger: desiredTilt=Mathf.Sin(Time.time*22)*8;desiredWing=13;break;
                case AshwingBeat.Transition: desiredWing=-21;desiredTilt=4;break;
                case AshwingBeat.Dead: desiredTilt=deathAge*26;desiredWing=deathAge*18;break;
            }
            poseAngle=Mathf.Lerp(poseAngle,desiredTilt,1-Mathf.Exp(-Time.deltaTime*13));
            wingAngle=Mathf.Lerp(wingAngle,desiredWing,1-Mathf.Exp(-Time.deltaTime*12));
            rigBaseRotation=rigRoot.localRotation;rigBasePosition=rigRoot.localPosition;
            rigRoot.localRotation=rigBaseRotation*Quaternion.Euler(0,0,poseAngle);
            rigRoot.localPosition=rigBasePosition+Vector3.up*(Mathf.Sin(Time.time*3.1f)*.055f-(deathAge>0?deathAge*.4f:0));
            if(wingBones!=null)for(int i=0;i<wingBones.Length;i++)
            {
                if(wingBones[i]==null)continue;
                wingBaseRotation[i]=wingBones[i].localRotation;
                wingBones[i].localRotation=wingBaseRotation[i]*Quaternion.Euler(0,0,wingAngle*(i<2?1:-1));
            }
            poseApplied=true;
            UpdateWingTrails();
            UpdateChargeFeathers();
        }

        private void UpdateWingTrails()
        {
            if(wingSprites==null)return;
            for(int i=0;i<wingTrails.Length&&i<wingSprites.Length;i++)
            {
                var sr=wingSprites[i];var trail=wingTrails[i];
                if(sr==null||trail==null)continue;
                Vector3 tip=sr.bounds.center;
                tip.x+=Mathf.Sign(tip.x-combat.BodyCenter.x)*sr.bounds.extents.x*.78f;
                tip.z=transform.position.z-.03f;
                bool emitting=deathAge<0&&combat.IsEngaged&&(combat.Enraged||combat.Beat==AshwingBeat.Dash||combat.Charge>.5f);
                if(!trail.emitting&&emitting)trail.Clear();
                trail.transform.position=tip;trail.emitting=emitting;
            }
        }
        private void UpdateChargeFeathers()
        {
            for(int i=0;i<chargeFeathers.Length;i++)
            {
                var sr=chargeFeathers[i];if(sr==null)continue;
                bool show=combat.Beat==AshwingBeat.Windup&&combat.Skill!=AshwingSkill.HuntingDash;
                if(combat.Skill==AshwingSkill.CinderRazor&&i!=1)show=false;
                sr.enabled=show;if(!show)continue;
                float t=combat.Charge;
                Vector2 direction=combat.Aim;
                Vector2 position=combat.Muzzle;
                if(combat.Skill==AshwingSkill.Ashfall)
                {
                    float angle=(45+i*45)*Mathf.Deg2Rad;
                    position=combat.BodyCenter+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(2.3f-t*.3f);
                    direction=Vector2.up;
                }
                else
                {
                    direction=Quaternion.Euler(0,0,(i-1)*25)*direction;
                    position+=direction*(.4f+t*.2f);
                }
                sr.transform.position=new Vector3(position.x,position.y,transform.position.z-.07f);
                sr.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
                sr.transform.localScale=Vector3.one*Mathf.Lerp(.10f,.24f,t)*effectsScale;
                var color=Color.Lerp(Gold,Hot,t);color.a=Mathf.Lerp(.2f,.95f,t);sr.color=color;
            }
        }

        private void RestorePose()
        {
            if(!poseApplied)return;
            if(rigRoot!=null){rigRoot.localRotation=rigBaseRotation;rigRoot.localPosition=rigBasePosition;}
            if(wingBones!=null)for(int i=0;i<wingBones.Length;i++)if(wingBones[i]!=null)wingBones[i].localRotation=wingBaseRotation[i];
            poseApplied=false;
        }

        private void UpdatePools(float dt)
        {
            foreach(var s in strokes)
            {
                if(s==null||!s.Used||s.Persistent)continue;
                s.Age+=dt;float t=Mathf.Clamp01(s.Age/s.Duration);
                if(t>=1){ReleaseStroke(s);continue;}
                var q=Quaternion.Euler(0,0,s.Spin*t);
                float scale=1+s.Expansion*(1-Mathf.Pow(1-t,3));
                for(int i=0;i<s.Count;i++)s.Points[i]=s.Center+q*(s.Original[i]*scale);
                for(int i=0;i<s.Count;i++)s.Renderer.SetPosition(i,s.Points[i]);
                var color=s.Color;color.a*=Mathf.Pow(1-t,1.4f);
                s.Renderer.startColor=color;s.Renderer.endColor=color;
                s.Renderer.widthMultiplier=s.Width*(1-t*.65f);
            }
            foreach(var m in motes)
            {
                if(!m.Renderer.enabled)continue;
                m.Age+=dt;float t=m.Age/m.Life;
                if(t>=1){m.Renderer.enabled=false;continue;}
                m.Velocity+=Vector3.up*dt*.45f;
                m.Renderer.transform.position+=m.Velocity*dt;
                m.Renderer.transform.Rotate(0,0,m.Spin*dt);
                m.Renderer.transform.localScale=Vector3.one*m.Scale*(1-t*.75f);
                var color=Color.Lerp(m.Color,new Color(.28f,.045f,.012f,0),t);color.a=m.Color.a*(1-t);
                m.Renderer.color=color;
            }
        }

        public Stroke Acquire()
        {
            foreach(var s in strokes)if(s!=null&&!s.Used)
            {s.Used=true;s.Persistent=true;s.Age=0;s.Renderer.enabled=true;s.Renderer.sortingOrder=25;return s;}
            return null;
        }
        public void ReleaseStroke(Stroke s){if(s==null)return;s.Used=false;s.Renderer.enabled=false;}
        public void Line(Stroke s,Vector2 start,Vector2 end,float width,Color color)
        {
            if(s==null)return;s.Count=2;s.Renderer.positionCount=2;
            s.Points[0]=new Vector3(start.x,start.y,transform.position.z-.05f);
            s.Points[1]=new Vector3(end.x,end.y,transform.position.z-.05f);
            s.Renderer.SetPosition(0,s.Points[0]);s.Renderer.SetPosition(1,s.Points[1]);
            Style(s,width,color);
        }
        public void Arc(Stroke s,Vector2 center,float radius,float startAngle,float sweep,float width,Color color,float flatten=1)
        {
            if(s==null)return;s.Count=49;s.Renderer.positionCount=s.Count;
            for(int i=0;i<s.Count;i++)
            {
                float a=(startAngle+sweep*i/(s.Count-1))*Mathf.Deg2Rad;
                s.Points[i]=new Vector3(center.x+Mathf.Cos(a)*radius,center.y+Mathf.Sin(a)*radius*flatten,transform.position.z-.04f);
                s.Renderer.SetPosition(i,s.Points[i]);
            }
            Style(s,width,color);
        }
        private void Style(Stroke s,float width,Color color)
        {width*=effectsScale;s.Renderer.widthMultiplier=width;s.Width=width;s.Color=color;s.Renderer.startColor=color;s.Renderer.endColor=color;}
        private static void Animate(Stroke s,Vector2 center,float life,float expansion,float spin)
        {
            if(s==null)return;s.Persistent=false;s.Duration=life;s.Expansion=expansion;s.Spin=spin;
            s.Center=new Vector3(center.x,center.y,s.Points[0].z);
            for(int i=0;i<s.Count;i++)s.Original[i]=s.Points[i]-s.Center;
        }
        public void RingBurst(Vector2 center,float radius,float life,Color color,int count=2)
        {
            for(int i=0;i<count;i++)
            {
                var s=Acquire();Arc(s,center,radius*effectsScale*(.55f+i*.12f),i*117,245,.075f,color);
                Animate(s,center,life,1.25f,i%2==0?35:-35);
            }
        }
        public void Spark(Vector2 position,Vector2 velocity,float scale,float life,Color color)
        {
            foreach(var m in motes)if(!m.Renderer.enabled)
            {
                m.Age=0;m.Life=life;m.Color=color;m.Velocity=velocity;
                m.Scale=scale*effectsScale;m.Spin=Random.Range(-160,160);
                m.Renderer.transform.position=new Vector3(position.x,position.y,transform.position.z-.08f);
                m.Renderer.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg);
                m.Renderer.transform.localScale=Vector3.one*m.Scale;
                m.Renderer.color=color;m.Renderer.enabled=true;return;
            }
        }
        public void Burst(Vector2 center,int count,float speed)
        {
            for(int i=0;i<count;i++)Spark(center+Random.insideUnitCircle*.12f,Random.insideUnitCircle*speed,
                Random.Range(.055f,.17f),Random.Range(.4f,.95f),i%3==0?Hot:Gold);
        }

        private void EmitWingFeathers(int count)
        {
            if(wingSprites==null)return;
            for(int i=0;i<count;i++)
            {
                var sr=wingSprites[Random.Range(0,wingSprites.Length)];if(sr==null||!sr.gameObject.activeInHierarchy)continue;
                Vector2 pos=sr.bounds.center;
                pos.x+=Mathf.Sign(pos.x-combat.BodyCenter.x)*sr.bounds.extents.x*.75f;
                Spark(pos,new Vector2(Random.Range(-.4f,.4f),Random.Range(.3f,1.1f)),Random.Range(.035f,.085f),.8f,Gold);
            }
        }
        private void UpdateCorona()
        {
            float energy=combat.Enraged?.52f:combat.IsEngaged?.12f:0;
            if(combat.Beat==AshwingBeat.Transition)energy=.85f;
            Vector2 center=combat.BodyCenter+Vector2.up*.28f;
            for(int i=0;i<3;i++)
            {
                Color c=i==0?Cinder:Gold;c.a=energy*(.8f+.2f*Mathf.Sin(Time.time*2+i));
                Arc(corona[i],center,(2.3f+i*.16f)*effectsScale,Time.time*(i==1?-10:8)+i*120,76,.045f,c,.91f);
            }
        }
        public void BeginTelegraph(AshwingSkill skill)
        {
            EndTelegraph();
            for(int i=0;i<3;i++)telegraph[i]=Acquire();
            if(chargeClip!=null){chargeVoice.clip=chargeClip;chargeVoice.volume=volume*.6f;chargeVoice.pitch=skill==AshwingSkill.HuntingDash?.8f:1.15f;chargeVoice.Play();}
        }
        public void AimTelegraph(Vector2 origin,Vector2 direction,float spread,float progress)
        {
            for(int i=0;i<3;i++)
            {
                var dir=(Vector2)(Quaternion.Euler(0,0,(i-1)*spread)*direction);
                Color color=Gold;color.a=(spread==0&&i!=1)?0:Mathf.Lerp(.10f,.52f,progress);
                Line(telegraph[i],origin+dir*.3f,origin+dir*Mathf.Lerp(2.4f,8f,progress),.045f,color);
            }
            if(Random.value<Time.deltaTime*26)
            {
                Vector2 p=origin+Random.insideUnitCircle.normalized*Mathf.Lerp(1.2f,.25f,progress);
                Spark(p,(origin-p)*3,.05f,.23f,Hot);
            }
        }
        public void DashTelegraph(Vector2 start,Vector2 end,float radius,float progress)
        {
            Color c=Gold;c.a=Mathf.Lerp(.10f,.65f,progress);
            Line(telegraph[0],start+Vector2.up*radius,end+Vector2.up*radius,.04f,c);
            Line(telegraph[1],start-Vector2.up*radius,end-Vector2.up*radius,.04f,c);
            c.a*=.32f;Line(telegraph[2],start,end,.14f,c);
        }
        public void EndTelegraph()
        {
            for(int i=0;i<telegraph.Length;i++){ReleaseStroke(telegraph[i]);telegraph[i]=null;}
            foreach(var feather in chargeFeathers)if(feather!=null)feather.enabled=false;
            if(chargeVoice!=null)chargeVoice.Stop();
        }
        public void Release(Vector2 origin,Vector2 aim,bool fan)
        {
            RingBurst(origin,fan?.65f:.38f,.3f,Hot,2);Burst(origin,fan?14:8,3);
            float a=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
            var s=Acquire();Arc(s,origin,.8f*effectsScale,a-70,140,.14f,Gold);Animate(s,origin,.26f,1.5f,fan?30:0);
            Play(featherClip,.8f,fan?.84f:1.08f);Shake(.1f,.22f);
        }
        private void WingSlash(Vector2 direction)
        {
            Vector2 c=combat.BodyCenter;
            float a=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            var s=Acquire();Arc(s,c,1.6f*effectsScale,a+105,150,.1f,Gold,.72f);Animate(s,c,.26f,.15f,0);
            Spark(c,-direction*3+Random.insideUnitCircle,.18f,.28f,Cinder);
        }
        public void Engage(){Play(callClip,.8f,.9f);RingBurst(combat.BodyCenter,1.8f,.8f,Cinder);}
        public void DashLaunch(Vector2 dir){Burst(combat.BodyCenter,22,4);WingSlash(dir);Play(dashClip,1,.8f);Shake(.16f,.55f);}
        public void DashBrake(Vector2 dir){Burst(combat.BodyCenter-dir*.7f,14,2);RingBurst(combat.BodyCenter,1.4f,.45f,Gold);}
        public void RainRelease(){RingBurst(combat.BodyCenter,2.3f,.75f,Gold,3);Burst(combat.BodyCenter,28,5);Play(callClip,.8f,.7f);}
        public void PhaseTransition(){phaseBurstAt=Time.time+.9f;Play(phaseClip,1,.88f);RingBurst(combat.BodyCenter,1.1f,1.15f,Cinder,3);}
        public void Parried(){flash=1;EndTelegraph();RingBurst(combat.BodyCenter,1.2f,.45f,Hot,3);Burst(combat.BodyCenter,24,5);Play(parryClip,.9f,1);Shake(.2f,.75f);}
        public void Hit(){flash=.8f;if(Time.time>hitSoundTime){hitSoundTime=Time.time+.12f;Play(impactClip,.32f,1.15f);}Burst(combat.BodyCenter,4,1.5f);}
        public void Impact(Vector2 position,bool parried=false){RingBurst(position,.45f,.3f,parried?Hot:Gold);Burst(position,9,2.5f);Play(parried?parryClip:impactClip,.35f,1.2f);}
        public void Death()
        {
            EndTelegraph();deathAge=0;phaseBurstAt=-1;
            foreach(var c in corona)ReleaseStroke(c);
            RingBurst(combat.BodyCenter,2.2f,1.1f,Gold,3);Burst(combat.BodyCenter,60,4.2f);
            Play(callClip,1,.55f);Shake(.35f,.8f);
        }
        private void Play(AudioClip clip,float gain,float pitch)
        {
            if(voice==null||clip==null)return;
            var camera=Camera.main;if(camera!=null && Vector2.Distance(camera.transform.position,transform.position)>35)return;
            voice.pitch=pitch+Random.Range(-.035f,.035f);voice.PlayOneShot(clip,volume*gain);
        }
        private void Shake(float duration,float gain)
        {
            if(AdvancedCameraController2D.Instance!=null)
                AdvancedCameraController2D.Instance.Shake(duration,shakeStrength*gain,24);
        }
        private void OnDisable()
        {
            RestorePose();EndTelegraph();
            if(effectRoot!=null)effectRoot.gameObject.SetActive(false);
        }
        private void OnEnable(){if(effectRoot!=null)effectRoot.gameObject.SetActive(true);}
        private void OnDestroy(){if(effectRoot!=null)Destroy(effectRoot.gameObject);}
    }
}
