using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    public int level = 1;
    public float moveSpeedCache= 0.5f;
    public float moveSpeed = 0.5f;
    public float attackSpeed = 2.0f;
    public int attackPower = 5;
    private Animator animCtrl = null;
    private List<Vocabulary> vocList = new List<Vocabulary>();
    private Slider hpSlider = null;
    private Text[] vocText;
    private bool arrive = false;//是否到了 塔下
    private GameController gameCtrl = null;
    private bool damageEventFired = false;
    private Coroutine damageFallbackCoroutine = null;
    private Coroutine deathFallbackCoroutine = null;
    void Awake()
    {
        animCtrl = GetComponent<Animator>();
        ReinitAnimator();
    }

    /// <summary>
    /// 重新初始化动画：进入 move 状态。
    /// 动画结束事件现在由 RunAnimSetup 在生成动画片段时直接写入，
    /// 避免运行时调用编辑器 API，也避免 clip 长度变化后事件点错位。
    /// </summary>
    public void ReinitAnimator()
    {
        if (animCtrl == null) return;
        animCtrl.SetBool(Conf.moveAnim, true);
    }

    private void Start()
    {
        if (2 == gameCtrl.levelType)
        {
            //让单词每隔几秒显现一下
            InvokeRepeating("ShowAndFade", 3.0f, 3.0f);
        }
        else if (3 == gameCtrl.levelType)
        {
            //不显示单词
            vocText[0].enabled = false;
        }
    }
    void Update()
    {
        //Enemy移动
        var animInfo = animCtrl.GetCurrentAnimatorStateInfo(0);
        if (animInfo.IsName("move"))
        {
            if(animCtrl.speed != 0)
            {
                if (animCtrl.speed != moveSpeed)
                    animCtrl.speed = moveSpeed;
                //如果没有移动到塔下 继续前进
                if (!arrive)
                {
                    transform.position += -transform.right * moveSpeed * Time.deltaTime;
                }
            }
        }
        else
        {
            //if (animCtrl.speed != 1)
            //    animCtrl.speed = 1;
            if (animInfo.IsName("death"))
            {
                animCtrl.SetBool(Conf.deathAnim, false);
                if (animInfo.normalizedTime >= 1.0f)
                {
                    gameCtrl.UpdateRemainEnemyList(this);
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                }
            }
        }
    }
    // 如果另一个碰撞器 2D 进入了触发器，则调用 OnTriggerEnter2D (仅限 2D 物理)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            arrive = true;
            //如果已经走到塔下 停止前进 并攻击
            animCtrl.SetBool(Conf.moveAnim, false);
            animCtrl.SetBool(Conf.attackAnim, true);
        }
    }
    public void InitEnemyData(GameController ctrl)
    {
        gameCtrl = ctrl;
        hpSlider = GetComponentInChildren<Slider>();
        vocText = GetComponentsInChildren<Text>();
        if (gameCtrl.vocList.Count > 0)
        {
            if (level > gameCtrl.vocList.Count)
                level = gameCtrl.vocList.Count;
            for (int i = 0; i < level; i++)
            {
                vocList.Add(gameCtrl.vocList[0]);
                gameCtrl.vocList.RemoveAt(0);
            }

            hpSlider.maxValue = level;
            hpSlider.value = hpSlider.maxValue;
            hpSlider.wholeNumbers = true;
            vocText[0].text = vocList[0].en;
            vocText[1].text = vocList[0].zh;
        }
    }

    /// <summary>
    /// 换皮：鸣人模式下小怪显示佐助形象，佐助模式下显示鸣人形象（各 3 个变体随机）。
    /// 由 GameController 在单词窗口挂好之后调用，这样不会影响 vocText 的采集。
    /// </summary>
    public EnemySkinController ApplyCharacterSkin()
    {
        EnemySkinController skin = GetComponent<EnemySkinController>();
        if (skin == null)
            skin = gameObject.AddComponent<EnemySkinController>();
        skin.Apply(this);
        return skin;
    }

    void AttackAnimEndCallBack()
    {
        StartCoroutine(WaitAttack());
        //塔受击减血
        gameCtrl.Damage(attackPower);
    }
    IEnumerator WaitAttack()
    {
        animCtrl.SetBool(Conf.attackAnim, false);
        yield return new WaitForSeconds(attackSpeed);
        animCtrl.SetBool(Conf.attackAnim, true);
    }
    void DamageAnimEndCallBack()
    {
        if (damageEventFired) return;
        damageEventFired = true;
        ApplyDamageResult();
    }

    private void ApplyDamageResult()
    {
        animCtrl.SetBool(Conf.damageAnim, false);
        if (vocList.Count > 0)
            vocList.RemoveAt(0);
        hpSlider.value = vocList.Count;
        if (hpSlider.value < 1)
        {
            //如果血条小于1播放死亡动画
            animCtrl.SetBool(Conf.deathAnim, true);
            var vocWin = transform.Find("VocWin");
            if (vocWin)
                vocWin.gameObject.SetActive(false);
            // 保险：1.5 秒后还没被 Update/事件销毁，强制销毁。
            if (deathFallbackCoroutine != null)
                StopCoroutine(deathFallbackCoroutine);
            deathFallbackCoroutine = StartCoroutine(DeathFallback());
        }
        else
        {
            vocText[0].text = vocList[0].en;
            vocText[1].text = vocList[0].zh;
            animCtrl.SetBool(Conf.moveAnim, true);
        }
        gameCtrl.UpdateRemainVocCount();
    }

    void DeathAnimEndCallBack()
    {
        //如果敌人挂了从列表中移除
        Destroy(gameObject);
        gameCtrl.UpdateRemainEnemyList(this);
    }

    public void Damage()
    {
        damageEventFired = false;
        animCtrl.SetBool(Conf.damageAnim, true);
        animCtrl.SetBool(Conf.moveAnim, false);
        // 保险：如果动画事件没触发，0.4 秒后强制走伤害结算。
        if (damageFallbackCoroutine != null)
            StopCoroutine(damageFallbackCoroutine);
        damageFallbackCoroutine = StartCoroutine(DamageFallback());
    }

    private IEnumerator DamageFallback()
    {
        yield return new WaitForSeconds(0.4f);
        if (!damageEventFired)
        {
            Debug.LogWarning("[Enemy] damage 动画事件未触发，启用强制结算：" + gameObject.name);
            ApplyDamageResult();
        }
        damageFallbackCoroutine = null;
    }

    private IEnumerator DeathFallback()
    {
        yield return new WaitForSeconds(1.5f);
        if (gameObject != null)
        {
            Debug.LogWarning("[Enemy] death 未正常销毁，启用强制销毁：" + gameObject.name);
            gameCtrl.UpdateRemainEnemyList(this);
            Destroy(gameObject);
        }
        deathFallbackCoroutine = null;
    }
    //被冰冻技能击中时冻结
    public void Freeze()
    {
        StartCoroutine(stopAll());
    }
    private IEnumerator stopAll()
    {
        var oldSpeed = moveSpeedCache;// animCtrl.speed;
        animCtrl.speed = 0;
        yield return new WaitForSeconds(gameCtrl.skillTime);//冻结3秒
        animCtrl.speed = oldSpeed;
    }
    //被减速技能击中
    public void Slow()
    {
        StartCoroutine(SpeedDown());
    }
    private IEnumerator SpeedDown()
    {
        var mSpeed = moveSpeedCache;// moveSpeed;
        moveSpeed /= 4;
        yield return new WaitForSeconds(gameCtrl.skillTime);
        moveSpeed = mSpeed;
    }
    public void ShowVoc()
    {
        StartCoroutine(TipVoc());
    }
    private IEnumerator TipVoc()
    {
        vocText[0].enabled = true;
        yield return new WaitForSeconds(gameCtrl.skillTime);
        vocText[0].enabled = false;
    }
    private void ShowAndFade()
    {
        vocText[0].enabled = !vocText[0].enabled;
    }
}
