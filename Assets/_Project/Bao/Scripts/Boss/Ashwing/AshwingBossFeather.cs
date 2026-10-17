using UnityEngine;

namespace Bao.Bosses.Ashwing
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class AshwingBossFeather : MonoBehaviour
    {
        private Rigidbody2D body;
        private ParryableProjectileAdapter parry;
        private AshwingBossCombat owner;
        private AshwingBossVFX vfx;
        private SpriteRenderer feather;
        private TrailRenderer trail;
        private float remaining, emberTimer;
        private bool initialized, finished, reflected;
        private int baseDamage;
        public int Damage => baseDamage * (parry != null && parry.IsPlayerOwned ? 2 : 1);

        private void Awake()
        {
            body=GetComponent<Rigidbody2D>();feather=GetComponent<SpriteRenderer>();
            parry=ParryableProjectileAdapter.Ensure(this);
            body.gravityScale=0;body.freezeRotation=true;
            body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        }

        public void Initialize(AshwingBossCombat source,Vector2 direction,float speed,int damage,float lifetime)
        {
            owner=source;vfx=source.VFX;baseDamage=damage;remaining=lifetime;
            transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            body.linearVelocity=direction.normalized*speed;
            parry.TrackMotion(body.linearVelocity);
            trail=gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=vfx.Filament;
            trail.time=.22f;trail.startWidth=.13f*vfx.EffectsScale;trail.endWidth=0;trail.minVertexDistance=.06f;
            trail.numCapVertices=3;trail.sortingOrder=24;
            trail.startColor=new Color(1,.72f,.2f,.9f);trail.endColor=new Color(.8f,.045f,.01f,0);
            feather.sortingOrder=25;
            initialized=true;
        }

        private void Update()
        {
            if(!initialized||finished)return;
            if(owner==null||!owner.CanHurtPlayer){Finish(false);return;}
            remaining-=Time.deltaTime;
            if(remaining<=0){Finish(false);return;}
            Vector2 velocity=body.linearVelocity;
            if(velocity.sqrMagnitude>.01f)transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg);
            if(parry.HasBeenParried&&!reflected)
            {
                reflected=true;
                // The adapter flips local X for older projectiles. This projectile faces its velocity itself.
                var scale=transform.localScale;scale.x=Mathf.Abs(scale.x);transform.localScale=scale;
                feather.color=new Color(1,.95f,.65f);
                trail.startColor=new Color(1,1,.8f,1);
                vfx.Impact(transform.position,true);
            }
            emberTimer-=Time.deltaTime;
            if(emberTimer<=0)
            {
                emberTimer=.065f;
                vfx.Spark(transform.position,-velocity*.09f+Random.insideUnitCircle*.4f,.06f,.3f,
                    reflected?AshwingBossVFX.Hot:AshwingBossVFX.Gold);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if(!initialized||finished||owner==null||!owner.CanHurtPlayer)return;
            if(other.GetComponentInParent<AshwingBossCombat>()==owner && !parry.IsPlayerOwned)return;
            var player=other.GetComponentInParent<PlayerHealth>();
            if(player!=null)
            {
                if(parry.IgnorePlayerCollision)return;
                var result=PlayerHitResolver.TryApplyHitToPlayer(other,Damage,ParryableAttackType.Projectile,
                    parry,transform,body.linearVelocity);
                if(result!=IncomingAttackResolution.Parried && result!=IncomingAttackResolution.Ignored)Finish(true);
                return;
            }
            if(parry.IsPlayerOwned && parry.TryHandleEnemyCollision(other,Damage)){Finish(true);return;}
            if(owner.IsArenaSurface(other))Finish(true);
        }

        private void Finish(bool impact)
        {
            if(finished)return;finished=true;
            if(impact&&vfx!=null)vfx.Impact(transform.position);
            body.linearVelocity=Vector2.zero;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
