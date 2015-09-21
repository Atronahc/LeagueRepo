﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace OneKeyToWin_AIO_Sebby
{
    class Anivia
    {
        private Menu Config = Program.Config;
        public static Orbwalking.Orbwalker Orbwalker = Program.Orbwalker;

        private Spell E, Q, R, W;

        private float QMANA, WMANA, EMANA, RMANA;
        private float RCastTime = 0;
        private static GameObject QMissile, RMissile;
        private int FarmId;

        private Obj_AI_Hero Player {get{return ObjectManager.Player;}}

        public void LoadOKTW()
        {
            Q = new Spell(SpellSlot.Q, 1250);
            W = new Spell(SpellSlot.W, 950);
            E = new Spell(SpellSlot.E, 650);
            R = new Spell(SpellSlot.R, 650);

            Q.SetSkillshot(0.25f, 110f, 870f, false, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.6f, 1f, float.MaxValue, false, SkillshotType.SkillshotLine);
            R.SetSkillshot(2f, 400f, float.MaxValue, false, SkillshotType.SkillshotCircle);

            LoadMenuOKTW();

            Game.OnUpdate += Game_OnGameUpdate;
            Obj_AI_Base.OnDelete += Obj_AI_Base_OnDelete;
            Obj_AI_Base.OnCreate += Obj_AI_Base_OnCreate;
            Interrupter2.OnInterruptableTarget += Interrupter2_OnInterruptableTarget;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Drawing.OnDraw += Drawing_OnDraw;
        }

        private void Interrupter2_OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (Config.Item("inter", true).GetValue<bool>() && W.IsReady() && sender.IsValidTarget(W.Range))
                W.Cast(sender);
        }

        private void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            var Target = (Obj_AI_Hero)gapcloser.Sender;
            if (Q.IsReady() && Config.Item("AGCQ", true).GetValue<bool>())
            {
                if (Target.IsValidTarget(Q.Range))
                {
                    Q.Cast(Target);
                    Program.debug("AGC Q");
                }
            }
            else if (W.IsReady() && Config.Item("AGCW", true).GetValue<bool>())
            {
                if (Target.IsValidTarget(W.Range))
                {
                    W.Cast(ObjectManager.Player.Position.Extend(Target.Position, 50), true);
                }
            }
        }

        private void LoadMenuOKTW()
        {
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("LCE", "Lane clear E", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("farmR", "Lane clear R", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("Mana", "LaneClear Mana", true).SetValue(new Slider(60, 100, 30)));

            Config.SubMenu(Player.ChampionName).SubMenu("AntiGapcloser").AddItem(new MenuItem("AGCQ", "Q", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("AntiGapcloser").AddItem(new MenuItem("AGCW", "W", true).SetValue(false));

            Config.SubMenu(Player.ChampionName).AddItem(new MenuItem("inter", "OnPossibleToInterrupt W")).SetValue(true);
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu(Player.ChampionName).SubMenu("Haras Q").AddItem(new MenuItem("haras" + enemy.BaseSkinName, enemy.BaseSkinName).SetValue(true));

            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("wRange", "W range", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("eRange", "E range", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("rRange", "R range", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw only ready spells", true).SetValue(true));


            Config.SubMenu(Player.ChampionName).AddItem(new MenuItem("AACombo", "Disable AA if can use E", true).SetValue(true));
        }

        private void Obj_AI_Base_OnCreate(GameObject obj, EventArgs args)
        {
            if (obj.IsValid)
            {
                if (obj.Name == "cryo_FlashFrost_Player_mis.troy")
                    QMissile = obj;
                if (obj.Name.Contains("cryo_storm"))
                    RMissile = obj;
            }
        }

        private void Obj_AI_Base_OnDelete(GameObject obj, EventArgs args)
        {
            if (obj.IsValid)
            {
                if (obj.Name == "cryo_FlashFrost_Player_mis.troy")
                    QMissile = null;
                if (obj.Name.Contains("cryo_storm"))
                    RMissile = null;
            }
        }

        private void Orbwalking_BeforeAttack(LeagueSharp.Common.Orbwalking.BeforeAttackEventArgs args)
        {
            if (FarmId != args.Target.NetworkId)
                FarmId = args.Target.NetworkId;
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            if (Program.Combo && Config.Item("AACombo", true).GetValue<bool>())
            {
                if (!E.IsReady())
                    Orbwalking.Attack = true;

                else
                    Orbwalking.Attack = false;
            }
            else
                Orbwalking.Attack = true;

            if (Q.IsReady() && QMissile != null && QMissile.Position.CountEnemiesInRange(230) > 0)
                Q.Cast();
            

            if (Program.LagFree(0))
            {
                SetMana();
            }

            if (Program.LagFree(1) && !Player.IsWindingUp && R.IsReady() )
                LogicR();

            if (Program.LagFree(2) && !Player.IsWindingUp && W.IsReady() )
                LogicW();

            if (Program.LagFree(3) && !Player.IsWindingUp && Q.IsReady() && QMissile == null)
                LogicQ();

            if (Program.LagFree(4) && !Player.IsWindingUp && E.IsReady() )
                LogicE();

            
        }

        private void LogicW()
        {
            var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical);
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && t.IsValidTarget(W.Range) && Player.Mana > RMANA + EMANA + WMANA && W.GetPrediction(t).CastPosition.Distance(t.Position) > 100)
            {
                if (ObjectManager.Player.Position.Distance(t.ServerPosition) > Player.Position.Distance(t.Position))
                {
                    if (t.Position.Distance(ObjectManager.Player.ServerPosition) < t.Position.Distance(Player.Position))
                        Program.CastSpell(W, t);
                }
                else
                {
                    if (t.Position.Distance(ObjectManager.Player.ServerPosition) > t.Position.Distance(Player.Position) )
                        Program.CastSpell(W, t);
                }
            }
        }

        private void LogicR()
        {
            var t = TargetSelector.GetTarget(R.Range + 400, TargetSelector.DamageType.Physical);
            if (RMissile == null && t.IsValidTarget() && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None)
            {
                if (R.GetDamage(t) > t.Health)
                    R.Cast(t, true, true);
                else if (Player.Mana > RMANA + EMANA && E.GetDamage(t) * 2 + R.GetDamage(t) > t.Health)
                    R.Cast(t, true, true);
                if (Player.Mana > RMANA + EMANA + QMANA + WMANA)
                    R.Cast(t, true, true);
            }

            var allMinionsQ = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, R.Range + 400, MinionTypes.All);
            var Rfarm = R.GetCircularFarmLocation(allMinionsQ, R.Width);

            if (RMissile == null
                && ObjectManager.Player.ManaPercentage() > Config.Item("Mana", true).GetValue<Slider>().Value
                && Config.Item("farmR", true).GetValue<bool>() && Player.Mana > QMANA + EMANA
                && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear
                && Rfarm.MinionsHit > 2)
            {
                R.Cast(Rfarm.Position);
            }

            if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LaneClear && RMissile != null && (RMissile.Position.CountEnemiesInRange(450) == 0 || ObjectManager.Player.Mana < EMANA + QMANA))
            {
                R.Cast();
                Program.debug("combo");
            }
            else if (RMissile != null && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && (Rfarm.MinionsHit < 3 || Player.Mana < QMANA + EMANA + WMANA || Rfarm.Position.Distance(RMissile.Position) > 400))
            {
                R.Cast();
                Program.debug("farm");
            }
        }

        private void LogicE()
        {
            var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
            if (t.IsValidTarget())
            {

                var qCd = Q.Instance.CooldownExpires - Game.Time;
                var rCd = R.Instance.CooldownExpires - Game.Time;
                if (ObjectManager.Player.Level < 7)
                    rCd = 10;
                //debug("Q " + qCd + "R " + rCd + "E now " + E.Instance.Cooldown);
                var eDmg = E.GetDamage(t);
                if (t.HasBuff("chilled"))
                {
                    eDmg = 2 * eDmg;
                }
                if (eDmg > t.Health)
                    E.Cast(t, true);
                else if ((t.HasBuff("chilled") || (qCd > E.Instance.Cooldown - 1 && rCd > E.Instance.Cooldown - 1)) && Program.Combo && ObjectManager.Player.Mana > RMANA + EMANA && QMissile == null)
                {
                    if (RMissile == null && R.IsReady())
                        R.Cast(t, true, true);
                    E.Cast(t, true);
                }
                else if (t.HasBuff("chilled") && (Program.Farm && Player.Mana > RMANA + EMANA + QMANA + WMANA) && !Player.UnderTurret(true) && QMissile == null)
                {
                    if (RMissile == null && R.IsReady())
                        R.Cast(t, true, true);
                    E.Cast(t, true);
                }
                else if (t.HasBuff("chilled") && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                {
                    E.Cast(t, true);
                }
            }
            farmE();
        }

        private void LogicQ()
        {
            var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
            if (t.IsValidTarget())
            {
                var qDmg = Q.GetDamage(t);

                if (qDmg > t.Health)
                    Program.CastSpell(Q, t);
                else if (Program.Combo && ObjectManager.Player.Mana > EMANA + QMANA - 10)
                    Program.CastSpell(Q, t);
                else if ((Program.Farm && ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA) && !ObjectManager.Player.UnderTurret(true) && OktwCommon.CanHarras())
                {
                    foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range) && Config.Item("haras" + enemy.BaseSkinName).GetValue<bool>()))
                        Program.CastSpell(Q, enemy);
                    
                }

                else if ((Program.Combo || Program.Farm) && ObjectManager.Player.Mana > RMANA + QMANA + EMANA)
                {
                    foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range)))
                    {
                        if (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                         enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                         enemy.HasBuffOfType(BuffType.Taunt) || enemy.HasBuffOfType(BuffType.Slow) || enemy.HasBuff("Recall"))
                        {
                            Q.Cast(enemy, true);
                        }
                    }
                }
            }
        }
        private void farmE()
        {
            if (Config.Item("LCE", true).GetValue<bool>() && ObjectManager.Player.Mana > QMANA + EMANA + WMANA && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && !Orbwalking.CanAttack() && ObjectManager.Player.ManaPercentage() > Config.Item("Mana", true).GetValue<Slider>().Value)
            {

                var mobs = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                if (mobs.Count > 0)
                {
                    var mob = mobs[0];
                    E.Cast(mob, true);
                    return;
                }

                var minions = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth);
                foreach (var minion in minions.Where(minion => minion.Health > ObjectManager.Player.GetAutoAttackDamage(minion) && FarmId != minion.NetworkId))
                {
                    var eDmg = E.GetDamage(minion);
                    if (minion.HasBuff("chilled"))
                        eDmg = 2 * eDmg;

                    if (minion.Health < eDmg * 0.9)
                        E.Cast(minion);
                }
            }
        }
        private void SetMana()
        {
            if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.2)
            {
                QMANA = 0;
                WMANA = 0;
                EMANA = 0;
                RMANA = 0;
                return;
            }
            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;
            if (!R.IsReady())
                RMANA = QMANA - ObjectManager.Player.Level * 2;
            else
                RMANA = R.Instance.ManaCost;

            
        }
        private void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("qRange", true).GetValue<bool>())
            {
                if (Config.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (Q.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
            }
            if (Config.Item("wRange", true).GetValue<bool>())
            {
                if (Config.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (W.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
            }
            if (Config.Item("eRange", true).GetValue<bool>())
            {
                if (Config.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (E.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow, 1, 1);
            }
            if (Config.Item("rRange", true).GetValue<bool>())
            {
                if (Config.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (R.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
            }
        }
    }
}
