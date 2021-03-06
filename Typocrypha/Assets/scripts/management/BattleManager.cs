﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// manages battle sequences
public class BattleManager : MonoBehaviour {
	public static BattleManager main = null; // static instance accessible globally (try not to use this)
	public SpellDictionary spellDict; // spell dictionary object
	public GameObject enemy_prefab; // prefab for enemy object
    public GameObject ally_prefab; //prefab for ally object
    public DisplayAlly ally_left; // left ally UI
    public DisplayAlly ally_right; // right ally UI
    public ChatDatabase chat; //Database containing chat lines
    public GameObject battleLogCast; //Casting log object and Associated reference to store
    public TextMesh logCastText;
    public TextMesh logCastInfo;
    public GameObject battleLogTalk; //talk log object and Associated reference to store
    public TextMesh logTalkText;
    public TextMesh logTalkInfo;
        private MeshRenderer[] battleLogMeshes = new MeshRenderer[4];
        private SpriteRenderer[] battleLogSprites = new SpriteRenderer[4];
	public EnemyChargeBars charge_bars; // creates and mananges charge bars
	public EnemyStaggerBars stagger_bars; // creates and manages stagger bars
	public CooldownList cooldown_list; // creates and manages player's cooldowns
	public Transform target_ret; // shows where target is
	public GameObject dialogue_box; // text box for dialogue
	public float enemy_spacing; // space between enemies

	public bool pause; // is battle paused?
	public Enemy[] enemy_arr; // array of Enemy components (size 3)
    public int target_ind; // index of currently targeted enemy
    public ICaster[] player_arr = { null, Player.main, null }; // array of Player and allies (size 3)
    public int player_ind = 1;
	public int enemy_count; // number of enemies in battle
	BattleScene curr_battle; // current battle scene

	public GameObject popper; //object that handles pop-up graphics (GraphicsPopper)
	Popper popp; //holds popper script component
	const float POP_TIMER = 2f; //pop-ups last this many seconds long
	Vector3 DMGNUM_OFFSET = new Vector3 (-1,0,0); //where the damage number should be
	Vector3 UNDER_OFFSET = new Vector3 (-1,-1,0); //where something under the damage num should be
	Vector3 OVER_OFFSET = new Vector3 (-1,1,0); //where something over the damage num should be

	void Awake() {
		if (main == null) main = this;
		pause = false;

		popp = popper.GetComponent<Popper>();
	}

	// start battle scene
	public void startBattle(BattleScene scene) {
		Debug.Log ("Battle!");
		curr_battle = scene;

        //CREATE ENEMIES//

		enemy_arr = new Enemy[3];
		enemy_count = scene.enemy_stats.Length;
		charge_bars.initChargeBars ();
		stagger_bars.initStaggerBars ();
		for (int i = 0; i < scene.enemy_stats.Length; i++) {
			GameObject new_enemy = GameObject.Instantiate (enemy_prefab, transform);
			new_enemy.transform.localScale = new Vector3 (1, 1, 1);
			new_enemy.transform.localPosition = new Vector3 (i * enemy_spacing, 0, 0);
			enemy_arr [i] = new_enemy.GetComponent<Enemy> ();
            enemy_arr[i].field = this; //Give enemey access to field (for calling spellcasts)
            enemy_arr [i].initialize (scene.enemy_stats [i]); //sets enemy stats (AND INITITIALIZES ATTACKING AND AI)
            enemy_arr [i].position = i;      //Log enemy position in field
            enemy_arr[i].bars = charge_bars; //Give enemy access to charge_bars
			Vector3 bar_pos = new_enemy.transform.position;
			bar_pos.Set (bar_pos.x, bar_pos.y + 1, bar_pos.z);
			charge_bars.makeChargeMeter(i, bar_pos);
			stagger_bars.makeStaggerMeter (i, bar_pos);
		}

        //CREATE ALLIES//

        for(int i = 0; i < scene.ally_stats.Length; ++i)
        {
            GameObject new_ally = GameObject.Instantiate(ally_prefab, transform);
            new_ally.transform.localScale = new Vector3(1, 1, 1);
            new_ally.transform.localPosition = new Vector3(1, 0, 0);
            Ally a = new_ally.GetComponent<Ally>();
            a.setStats(scene.ally_stats[i]);
            if (i == 1)
            {
                ally_right.setAlly(a);
                a.transform.position = ally_right.transform.position;
                ++i;
            }
            else
            {
                ally_left.setAlly(a);
                a.transform.position = ally_left.transform.position;
            }
            a.position = i;
            player_arr[i] = a;
        }

        //INITIALIZE BATTLELOG STUFF//

        const int numElements = 2;
        System.Array.Copy(battleLogCast.GetComponentsInChildren<MeshRenderer>(true), 0, battleLogMeshes, 0, numElements);
        System.Array.Copy(battleLogCast.GetComponentsInChildren<SpriteRenderer>(true), 0, battleLogSprites, 0, numElements);
        System.Array.Copy(battleLogTalk.GetComponentsInChildren<MeshRenderer>(true), 0, battleLogMeshes, numElements, numElements);
        System.Array.Copy(battleLogTalk.GetComponentsInChildren<SpriteRenderer>(true), 0, battleLogSprites, numElements, numElements);

        //FINISH//

        pause = false;
		target_ind = 0;
		AudioPlayer.main.playMusic (MusicType.BATTLE, scene.music_tracks[0]);
	}

    //removes all enemies and charge bars
    public void stopBattle()
    {
        pause = true;
        foreach (Enemy enemy in enemy_arr)
        {
            if (enemy != null) GameObject.Destroy(enemy.gameObject);
        }
        enemy_arr = null;
        cooldown_list.removeAll();
        charge_bars.removeAll();
        stagger_bars.removeAll();
    }

    // check if player switches targets or attacks
    void Update() {
		if (Input.GetKeyDown (KeyCode.BackQuote)) // toggle pause
			pause = !pause;
		if (pause) return;
		int old_ind = target_ind;

        //TARGET RETICULE CODE 

		// move target left or right
		if (Input.GetKeyDown (KeyCode.LeftArrow)) --target_ind;
		if (Input.GetKeyDown (KeyCode.RightArrow)) ++target_ind;
		// fix if target is out of bounds
		if (target_ind < 0) target_ind = 0;
		if (target_ind > 2) target_ind = 2;
		// move target reticule
		target_ret.localPosition = new Vector3 (target_ind * enemy_spacing, -1, 0);
		// play effect sound if target was moved
		if (old_ind != target_ind) AudioPlayer.main.playSFX(0, SFXType.UI, "sfx_enemy_select");

        //SPELLBOOK CODE

        // go to next page if down is pressed
        if (Input.GetKeyDown(KeyCode.DownArrow) && spellDict.pageDown())
        {
            //play page change SFX here
        }
        // go to last page if down is pressed
        if (Input.GetKeyDown(KeyCode.UpArrow) && spellDict.pageUp())
        {
            //play page change SFX here
        }
    }

    //CASTING CODE//---------------------------------------------------------------------------------------------------------------------------------------//

	// attack currently targeted enemy with spell
	public void attackCurrent(string spell) {
        //Can attack dead enemies now, just wont cast spell at them
		StartCoroutine (pauseAttackCurrent (spell));
    }

	// pause for player attack, play animations, unpause
	IEnumerator pauseAttackCurrent(string spell){

        pause = true;

        //SPELL PARSING//

        SpellData s;
        //Send spell, Enemy state, and target index to parser and caster
        CastStatus status = spellDict.parse(spell.ToLower(), out s);
        
        //DIMMING AND BATTLELOG//

        BattleEffects.main.setDim(true);
        Pair<bool[], bool[]> targetPattern = null;
        int allyPos = getAllyPosition(s.root);
        if (status == CastStatus.ALLYSPELL)
        {
            if(allyPos != -1)
            {
                battleLog(spell.ToUpper().Replace(' ', '-'), "ALLY", chat.getLine(player_arr[allyPos].Stats.name), Utility.String.FirstLetterToUpperCase(s.root));
                targetPattern = spellDict.getTargetPattern(s, enemy_arr, target_ind, player_arr, allyPos);
            }
            else
            {
                battleLog(spell.ToUpper().Replace(' ', '-'), "ALLY", "...", "Not here");
            }
        }          
        else if (status == CastStatus.SUCCESS)
        {
            battleLog(spell.ToUpper().Replace(' ', '-'), "PLAYER", chat.getLine("player_1"), player_arr[player_ind].Stats.name);
            targetPattern = spellDict.getTargetPattern(s, enemy_arr, target_ind, player_arr, player_ind);
        }
        else if (status == CastStatus.BOTCH)
        {
            battleLog(spell.ToUpper().Replace(' ', '-'), "PLAYER", chat.getLine("botch"), player_arr[player_ind].Stats.name);
        }
        else
        {
            battleLog(spell.ToUpper().Replace(' ', '-'), "PLAYER", "TODO: say something here", player_arr[player_ind].Stats.name);
        }
        if(targetPattern != null)
            raiseTargets(targetPattern.first, targetPattern.second);

        //BEGIN PAUSE//

        yield return new WaitForSeconds (1.5f);

        //CASTING//

        //Set last_cast
        ((Player)player_arr[player_ind]).Last_cast = s.ToString();
        //Cast/Botch/Cooldown/Fizzle, with associated effects and processing
        playerCast(spellDict, s, status);

        yield return new WaitForSeconds (1f);

        //END PAUSE//

        if (targetPattern != null)
            lowerTargets(targetPattern.first, targetPattern.second);
        stopBattleLog();
		BattleEffects.main.setDim (false);
		pause = false;
		updateEnemies();
	}

    //Casts from an ally position at target enemy_arr[target]: calls processCast on results
    public void NPC_Cast(SpellDictionary dict, SpellData s, int position, int target)
    {
        if(player_arr[position].Is_stunned)
        {
            Debug.Log(s.root + " cannot assist you because they are stunned!");
        }
        else if (((Ally)player_arr[position]).tryCast())
        {
            dict.startCooldown(s, (Player)player_arr[player_ind]);
            List<CastData> data = dict.cast(s, enemy_arr, target, player_arr, position);
            processCast(data, s);
        }
        else
        {
            Debug.Log(s.root + " is not ready to assist you yet!");
        }
    }
    //Casts from an enemy position: calls processCast on results
    public void enemyCast(SpellDictionary dict, SpellData s, int position)
    {
        pause = true; // pause battle for attack
        AudioPlayer.main.playSFX(1, SFXType.SPELL, "magic_sound");
        StartCoroutine(enemy_pause_cast(dict, s, position));

    }
    //Does the pausing for enemyCast (also does the actual cast calling)
    private IEnumerator enemy_pause_cast(SpellDictionary dict, SpellData s, int position)
    {
        Pair<bool[], bool[]>  targetPattern = spellDict.getTargetPattern(s, player_arr, 1, enemy_arr, position);
        BattleEffects.main.setDim(true, enemy_arr[position].GetComponent<SpriteRenderer>());
        raiseTargets(targetPattern.second, targetPattern.first);
        battleLog(s.ToString(), "ENEMY", chat.getLine("enemy_general_1"), enemy_arr[position].Stats.name);

        yield return new WaitForSeconds(1.5f);

        enemy_arr[position].startSwell();
        List<CastData> data = dict.cast(s, player_arr, player_ind, enemy_arr, position);
        processCast(data, s);

        yield return new WaitForSeconds(1f);

        stopBattleLog();
        lowerTargets(targetPattern.second, targetPattern.first);
        BattleEffects.main.setDim(false, enemy_arr[position].GetComponent<SpriteRenderer>());
        pause = false; // unpause
        enemy_arr[position].attack_in_progress = false;
        updateEnemies();
    }
    //Cast/Botch/Cooldown/Fizzle, with associated effects and processing
    //all animation and attack effects should be processed here
    //ONLY CALL FOR A PLAYER CAST (NOTE: NPC casts are routed through here as well)
    //Pre: CastStatus is generated by dict.Parse()
    private void playerCast(SpellDictionary dict, SpellData s, CastStatus status)
    {
        List<CastData> data;
        switch (status)//Switched based on caststatus
        {
            case CastStatus.SUCCESS:
                //Player Cast
                dict.startCooldown(s, (Player)player_arr[player_ind]);
                data = dict.cast(s, enemy_arr, target_ind, player_arr, player_ind);
                processCast(data, s);
                break;
            case CastStatus.BOTCH:
                data = dict.botch(s, enemy_arr, target_ind, player_arr, player_ind);
                Debug.Log("Botched cast: " + s.ToString());
                //Process the data here
                break;
            case CastStatus.ALLYSPELL:
                //NPC cast if appropriate
                if (player_arr[0].Stats.name.ToLower() == s.root)
                    NPC_Cast(dict, s, 0, target_ind);
                else if (player_arr[2].Stats.name.ToLower() == s.root)
                    NPC_Cast(dict, s, 2, target_ind);
                else
                {
                    Debug.Log(s.root + " isn't here!");
                }
                break;
            case CastStatus.ONCOOLDOWN:
                //Handle effects
                Debug.Log("Cast failed: " + s.root.ToUpper() + " is on cooldown for " + dict.getTimeLeft(s) + " seconds");
                break;
            case CastStatus.COOLDOWNFULL:
                //Handle effects
                Debug.Log("Cast failed: cooldownList is full!");
                break;
            case CastStatus.FIZZLE:
                //Handle effects
                break;
        }
    }
    //Method for processing CastData (where all the effects happen)
    //Called by Cast in the SUCCESS CastStatus case, possibly on BOTCH in the future
    //Can be used to process the cast of an enemy or ally, if implemented (put the AI loop in battlemanager)
    private void processCast(List<CastData> data, SpellData s)
    {
        //Process the data here
        foreach (CastData d in data)
        {
            if (d.Target.CasterType == ICasterType.PLAYER)
            {
                //Implement stuff for when player hits themselves
                if (d.isHit == false)//Spell misses
                {
                    Debug.Log(d.Caster.Stats.name + " missed " + d.Target.Stats.name + "!");
                    //Process miss graphics
                }
                else//Spell hits
                {
                    //Process hit graphics
					AudioPlayer.main.playSFX(1, SFXType.SPELL, "Cutting_SFX");

                    if (d.isCrit)//Spell is crit
                    {
                        Debug.Log(d.Caster.Stats.name + " scores a critical with " + s.ToString() + " on " + d.Target.Stats.name);
						AudioPlayer.main.playSFX(2, SFXType.BATTLE, "sfx_party_weakcrit_dmg");
                        //process crit graphics
                    }
                    Debug.Log(d.Target.Stats.name + " was hit for " + d.damageInflicted + " " + Elements.toString(d.element) + " damage x" + d.Target.Stats.getFloatVsElement(d.Target.BuffDebuff, d.element));
                    //Process elemental wk/resist/absorb/reflect graphics
                    //Process damage graphics
					BattleEffects.main.screenShake(0.5f, 0.1f);
                }
            }
            else if (d.Target.CasterType == ICasterType.NPC_ALLY)
            {
                Ally a = (Ally)d.Target;
                //Process hit graphics
                AudioPlayer.main.playSFX(1, SFXType.SPELL, "Cutting_SFX");
                AnimationPlayer.main.playAnimation(AnimationType.SPELL, "cut", a.transform.position, 1);
                if (d.isHit == false)//Spell misses
                {
                    Debug.Log(d.Caster.Stats.name + " missed " + d.Target.Stats.name + "!");
                    //Process miss graphics
                }
                else//Spell hits
                {
                    //Process hit graphics
                    AudioPlayer.main.playSFX(1, SFXType.SPELL, "Cutting_SFX");
                    AnimationPlayer.main.playAnimation(AnimationType.SPELL, "cut", a.transform.position, 1);

                    if (d.isCrit)//Spell is crit
                    {
                        Debug.Log(d.Caster.Stats.name + " scores a critical with " + s.ToString() + " on " + d.Target.Stats.name);
                        AudioPlayer.main.playSFX(2, SFXType.BATTLE, "sfx_enemy_weakcrit_dmg");
                        //process crit graphics
                        popp.spawnSprite("sprites/critical", POP_TIMER, a.transform.position + UNDER_OFFSET);
                    }
                    if (d.isStun)
                    {
                        //Process stun graphics
                        Debug.Log(d.Caster.Stats.name + " stuns " + d.Target.Stats.name);
                        AudioPlayer.main.playSFX(2, SFXType.BATTLE, "sfx_stagger");
                    }

                    Debug.Log(d.Target.Stats.name + " was hit for " + d.damageInflicted + " " + Elements.toString(d.element) + " damage x" + d.Target.Stats.getFloatVsElement(d.Target.BuffDebuff, d.element));

                    //Process elemental wk/resist/absorb/reflect graphics
                    switch (d.elementalData)
                    {
                        case Elements.vsElement.REFLECT:
                            popp.spawnSprite("sprites/reflect", POP_TIMER, a.transform.position + OVER_OFFSET);
                            break;
                        case Elements.vsElement.ABSORB:
                            popp.spawnSprite("sprites/absorb", POP_TIMER, a.transform.position + OVER_OFFSET);
                            break;
                        case Elements.vsElement.RESISTANT:
                            popp.spawnSprite("sprites/resistant", POP_TIMER, a.transform.position + OVER_OFFSET);
                            break;
                        case Elements.vsElement.WEAK:
                            popp.spawnSprite("sprites/weak", POP_TIMER, a.transform.position + OVER_OFFSET);
                            break;
                    }
                    //Process damage graphics
                    popp.spawnText(d.damageInflicted.ToString(), POP_TIMER, a.transform.position + DMGNUM_OFFSET);
                    if (d.damageInflicted > 0) BattleEffects.main.spriteShake(a.gameObject.transform, 0.5f, 0.1f);
                }

            }
            else//Target is an ENEMY
            {
                Enemy e = (Enemy)d.Target;
                if (d.isHit == false)//Spell misses
                {
                    Debug.Log(d.Caster.Stats.name + " missed " + d.Target.Stats.name + "!");
                    //Process miss graphics
                }
                else//Spell hits
                {
                    //Process hit graphics
					AudioPlayer.main.playSFX(1, SFXType.SPELL, "Cutting_SFX");
					AnimationPlayer.main.playAnimation(AnimationType.SPELL, "cut", e.transform.position, 1);

                    if (d.isCrit)//Spell is crit
                    {
                        Debug.Log(d.Caster.Stats.name + " scores a critical with " + s.ToString() + " on " + d.Target.Stats.name);
						AudioPlayer.main.playSFX(2, SFXType.BATTLE, "sfx_enemy_weakcrit_dmg");
						//process crit graphics
						popp.spawnSprite ("sprites/critical", POP_TIMER, e.transform.position + UNDER_OFFSET);
                    }
                    if (d.isStun)
                    {
                        //Process stun graphics
                        Debug.Log(d.Caster.Stats.name + " stuns " + d.Target.Stats.name);
                        AudioPlayer.main.playSFX(2, SFXType.BATTLE, "sfx_stagger");
                        charge_bars.Charge_bars[e.position].gameObject.transform.GetChild(0).GetComponent<Image>().color = new Color(1, 0.5F, 0);
                    }

					Debug.Log(d.Target.Stats.name + " was hit for " + d.damageInflicted + " " + Elements.toString(d.element) + " damage x" + d.Target.Stats.getFloatVsElement (d.Target.BuffDebuff, d.element));
                    
					//Process elemental wk/resist/absorb/reflect graphics
					switch (d.elementalData)
					{
						case Elements.vsElement.REFLECT:
							popp.spawnSprite ("sprites/reflect", POP_TIMER, e.transform.position + OVER_OFFSET);
							break;
						case Elements.vsElement.ABSORB:
							popp.spawnSprite ("sprites/absorb", POP_TIMER, e.transform.position + OVER_OFFSET);
							break;
						case Elements.vsElement.RESISTANT:
							popp.spawnSprite ("sprites/resistant", POP_TIMER, e.transform.position + OVER_OFFSET);
							break;
						case Elements.vsElement.WEAK:
							popp.spawnSprite ("sprites/weak", POP_TIMER, e.transform.position + OVER_OFFSET);
							break;
					}
					//Process damage graphics
					popp.spawnText (d.damageInflicted.ToString(), POP_TIMER, e.transform.position + DMGNUM_OFFSET);
					if (d.damageInflicted > 0) BattleEffects.main.spriteShake (e.gameObject.transform, 0.5f, 0.1f);
                }
            }
        }
        //Register unregistered keywords here
        bool [] regData = spellDict.safeRegister(s);
        //Process regData (for register graphics) here. 
        //format is bool [3], where regData[0] is true if s.element is new, regData[1] is true if s.root is new, and regData[2] is true if s.style is new

    }
    //Raises the targets (array val = true) above the dimmer level
    private void raiseTargets(bool[] enemy_r, bool[] player_r)
    {
        for (int i = 0; i < 3; ++i)
        {
            if (enemy_r[i])
                enemy_arr[i].enemy_sprite.sortingOrder = 10;
        }
    }
    //Lowers the targets (array val = true) below the dimmer level
    private void lowerTargets(bool[] enemy_r, bool[] player_r)
    {
        for (int i = 0; i < 3; ++i)
        {
            if (enemy_r[i])
                enemy_arr[i].enemy_sprite.sortingOrder = 0;
        }
    }
    //Returns true if ally with specified name is in the battle
    private bool allyIsPresent(string name)
    {
        if (player_arr[0] != null && player_arr[0].Stats.name.ToLower() == name.ToLower())
            return true;
        if (player_arr[2] != null && player_arr[2].Stats.name.ToLower() == name.ToLower())
            return true;
        return false;
    }
    //returns the position of ally with specified name (if in battle)
    private int getAllyPosition(string name)
    {
        if (player_arr[0] != null && player_arr[0].Stats.name.ToLower() == name.ToLower())
            return 0;
        if (player_arr[2] != null && player_arr[2].Stats.name.ToLower() == name.ToLower())
            return 2;
        return -1;
    }
    //Enable battle log UI state (call anywhere that the battlemanager pauses to cast)
    private void battleLog(string cast, string caster, string talk, string speaker)
    {
        battleLogCast.SetActive(true);
        battleLogTalk.SetActive(true);
        logCastText.text = cast;
        logCastInfo.text = caster;
        logTalkText.text = talk;
        logTalkInfo.text = speaker;
        for(int i = 0; i < battleLogSprites.Length; ++i)
        {
            battleLogSprites[i].sortingOrder = 10;
            battleLogMeshes[i].sortingOrder = 10;
        }
    }

    //Stop battle log UI (call after every pause to cast
    private void stopBattleLog()
    {
        battleLogCast.SetActive(false);
        battleLogTalk.SetActive(false);
        for (int i = 0; i < battleLogSprites.Length; ++i)
        {
            battleLogSprites[i].sortingOrder = 0;
            battleLogMeshes[i].sortingOrder = 0;
        }
    }

    //Updates death and opacity of enemies after pause in puaseAttackCurrent
    public void updateEnemies()
    {
        bool interrupted = checkInterrupts(); // check for interrupts
        if (interrupted) return;
        int curr_dead = 0;
        for (int i = 0; i < enemy_arr.Length; i++)
        {
            if (!enemy_arr[i].Is_dead)
                enemy_arr[i].updateCondition();
            if (enemy_arr[i].Is_dead) ++curr_dead;
        }
        if (curr_dead == enemy_count) // end battle if all enemies dead
        {
            Debug.Log("you win!");
            stopBattle();
            StartCoroutine(StateManager.main.nextSceneDelayed(2.0f));
        }
    }

    //INTERRUPT CODE//-------------------------------------------------------------------------------------------------------------------------------------//

    // checks and plays battle interruptions (returns true if an interrupt occured)
    bool checkInterrupts() {
		bool interrupted = false;
		// check for interrupt scenes
		for (int i = 0; i < curr_battle.interrupts.Length; ++i) {
			if (curr_battle.interrupts [i] == null) continue;
			BattleInterrupt binter = curr_battle.interrupts [i];
			// make sure all speaking members are still alive
			bool dead_speaker = false;
			for (int j = 0; j < 3; j++) {
				if (binter.who_speak [j] && enemy_arr [j].Is_dead) {
					curr_battle.interrupts [i] = null;
					dead_speaker = true;
				}
			}
			if (dead_speaker) continue;
			// check if condition is fulfilled
			if (binter.who_cond < 3) { // for enemy health
				Enemy curr_enemy = enemy_arr [binter.who_cond];
				if ((float)curr_enemy.Curr_hp / (float)curr_enemy.Stats.max_hp <= binter.health_cond) {
					Debug.Log ("enemy health condition fulfilled " + binter.who_cond + ":" + binter.health_cond);
					interrupted = true;
					curr_battle.interrupts [i] = null;
					StartCoroutine (playInterrupt (binter.scene));
				}
			} else { // for player health
				if ((float)Player.main.Curr_hp / (float)Player.main.Stats.max_hp <= binter.health_cond) {
					Debug.Log ("player health condition fulfilled:" + (float)Player.main.Curr_hp / (float)Player.main.Stats.max_hp);
					interrupted = true;
					curr_battle.interrupts [i] = null;
					StartCoroutine (playInterrupt (binter.scene));
				}
			}
		}
		return interrupted;
	}
	// plays a battle interrupt
	IEnumerator playInterrupt(CutScene scene) {
		pause = true;
		dialogue_box.SetActive (true);
		CutsceneManager.main.enabled = true;
		CutsceneManager.main.battle_interrupt = true;
		CutsceneManager.main.startCutscene (scene);
		yield return new WaitUntil (() => CutsceneManager.main.at_end);
		CutsceneManager.main.enabled = false;
		dialogue_box.SetActive (false);
		updateEnemies ();
		pause = false;
	}
}
