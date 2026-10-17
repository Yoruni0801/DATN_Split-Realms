using UnityEngine;

namespace Bao.Bosses.Ashwing
{
    /// <summary>Committed landing marker, parryable descending feather and short-lived cinder patch.</summary>
    public sealed class AshwingAshfall : MonoBehaviour, IParryable
    {
        private AshwingBossCombat owner;
        private AshwingBossVFX vfx;
        private AshwingBossVFX.Stroke rim, fill;
        private SpriteRenderer feather;
        private Vector2 landing, previous;
        private float age, warning, nextTick, emberTimer;
        private int damage;
        private bool landed, cancelled, struckInFlight;
        private const float FallTime=.30f, BurnTime=1.8f, Radius=.9f;
        public bool IsBurning => landed && !cancelled;

        public void Initialize(AshwingBossCombat source,Vector2 point,float delay,int hitDamage)
        {
            owner=source;vfx=source.VFX;landing=point;warning=delay;damage=hitDamage;
            transform.position=new Vector3(point.x,point.y,source.transform.position.z-.06f);
            rim=vfx.Acquire();fill=vfx.Acquire();
            var go=new GameObject("Descending cinder feather");go.transform.SetParent(transform,false);
            feather=go.AddComponent<SpriteRenderer>();feather.sprite=vfx.FeatherSprite;
            feather.color=AshwingBossVFX.Hot;feather.sortingOrder=26;
            go.transform.localScale=Vector3.one*.72f;
            go.transform.localRotation=Quaternion.Euler(0,0,-90);
            feather.enabled=false;
        }

        private void Update()
        {
            if(cancelled)return;
            if(owner==null||!owner.CanHurtPlayer){Cancel();return;}
            age+=Time.deltaTime;
            if(age<warning)
            {
                float t=age/warning;
                var c=AshwingBossVFX.Gold;c.a=.35f+.45f*t;
                vfx.Arc(rim,landing,Radius,0,360,.07f,c,.23f);
                c.a=.75f;vfx.Arc(fill,landing,Radius*.82f,-90,360*t,.045f,c,.23f);
                return;
            }
            if(!landed)
            {
                feather.enabled=true;
                float t=Mathf.Clamp01((age-warning)/FallTime);
                Vector2 position=landing+Vector2.up*(7*(1-t*t));
                if(previous==Vector2.zero)previous=position;
                if(!struckInFlight)
                {
                    var hit=Physics2D.CircleCast(previous,.3f,(position-previous).normalized,
                        Vector2.Distance(position,previous),owner.PlayerMask);
                    if(hit.collider!=null)
                    {
                        var result=PlayerHitResolver.TryApplyHitToPlayer(hit.collider,damage,ParryableAttackType.Projectile,
                            this,transform,Vector2.down);
                        struckInFlight=result!=IncomingAttackResolution.Ignored;
                        if(cancelled)return;
                    }
                }
                feather.transform.position=new Vector3(position.x,position.y,transform.position.z);
                previous=position;
                if(t<1)return;
                landed=true;nextTick=age+.65f;
                feather.transform.localScale=Vector3.one*.48f;
                feather.transform.localRotation=Quaternion.Euler(0,0,-64);
                vfx.Impact(landing);
                if(!struckInFlight)HitPlayer(damage,true);
                if(cancelled)return;
            }
            float burn=(age-warning-FallTime)/BurnTime;
            if(burn>=1){Cancel();return;}
            var glow=AshwingBossVFX.Cinder;glow.a=(1-burn)*.75f;
            vfx.Arc(rim,landing,Radius,0,360,.08f,glow,.23f);
            glow=AshwingBossVFX.Gold;glow.a=(1-burn)*.55f;
            vfx.Arc(fill,landing,Radius*.72f,Time.time*80,270,.10f,glow,.23f);
            emberTimer-=Time.deltaTime;
            if(emberTimer<=0)
            {
                emberTimer=.12f;
                vfx.Spark(landing+Vector2.right*Random.Range(-Radius,Radius),Vector2.up*Random.Range(.8f,1.6f),.07f,.55f,glow);
            }
            feather.color=Color.Lerp(AshwingBossVFX.Gold,new Color(.17f,.07f,.04f,0),burn);
            if(age>=nextTick){nextTick=age+.65f;HitPlayer(3,false);}
        }

        private void HitPlayer(int amount,bool impact)
        {
            // Low, visible damage volume matches the marked patch rather than the entire column.
            var hit=Physics2D.OverlapBox(landing+Vector2.up*.28f,new Vector2(Radius*2,.56f),0,owner.PlayerMask);
            if(hit==null)return;
            PlayerHitResolver.TryApplyHitToPlayer(hit,amount,impact?ParryableAttackType.Projectile:ParryableAttackType.Melee,
                this,transform,Vector2.down,impact);
        }

        public ParryResult OnParried(ParryContext context)
        {
            if(cancelled||age<warning||age>warning+FallTime+.08f)return ParryResult.Failed;
            if(vfx!=null)vfx.Impact(feather.transform.position,true);
            Cancel();
            return ParryResult.Successful(.08f,true);
        }
        private void Cancel()
        {
            if(cancelled)return;cancelled=true;
            Release();gameObject.SetActive(false);Destroy(gameObject);
        }
        private void Release(){if(vfx!=null){vfx.ReleaseStroke(rim);vfx.ReleaseStroke(fill);}rim=null;fill=null;}
        private void OnDisable(){Release();}
    }
}
