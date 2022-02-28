using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;
using Terraria.UI.Chat;
using System.IO;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ModLoader.IO;
using Terraria.Localization;
using Terraria.Utilities;
using System.Reflection;
using MonoMod.RuntimeDetour.HookGen;
using Microsoft.Xna.Framework.Audio;
using Terraria.Audio;
using Terraria.Graphics.Capture;
using Microsoft.Xna.Framework.Input;

namespace AmmoLock
{
	public class AmmoLock : Mod{
		public override void Load() {
			Hacc.Add();
		}
	}
	public class AmmoItem : GlobalItem
	{
		public override bool InstancePerEntity => true;
		public int ammoLock;
		public bool locked;
		public override GlobalItem Clone(Item item, Item itemClone) {
			AmmoItem myClone = (AmmoItem)base.Clone(item, itemClone);
			myClone.ammoLock = ammoLock;
			myClone.locked = locked;
			return myClone;
		}
		public AmmoItem() {
			ammoLock = 0;
			locked = false;
		}
		public override void Load(Item item, TagCompound tag) {
			ammoLock = tag.GetInt("ammoLock");
			locked = tag.GetBool("locked");
		}
		public override bool NeedsSaving(Item item) => ammoLock > 0;
		public override TagCompound Save(Item item) {
			TagCompound tag = new TagCompound();
			tag.Add("ammoLock",ammoLock);
			tag.Add("locked",locked);
			return tag;
		}
		public override bool CanRightClick(Item item) {
			if (Main.mouseRightRelease && item.useAmmo > 0) {
				if (ammoLock > 0 && (Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift))) {
					Main.PlaySound(SoundID.Tink);
					ammoLock = 0;
					locked = false;
					CombatText.NewText(Main.LocalPlayer.getRect(),Color.Pink,"Removed Ammo Lock"); 
				}
				else if (!Main.mouseItem.IsAir && Main.mouseItem.type > 0 && Main.mouseItem.stack > 0 && Main.mouseItem.ammo == item.useAmmo) {
					Main.PlaySound(SoundID.Tink);
					ammoLock = Main.mouseItem.type;
					if ((Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt))) {
						locked = true;
						CombatText.NewText(Main.LocalPlayer.getRect(),Color.LightGreen,$"Ammo Locked to {Main.mouseItem.Name}");
					}
					else {
						locked = false;
						CombatText.NewText(Main.LocalPlayer.getRect(),Color.LightGreen,$"Ammo Prioritized to {Main.mouseItem.Name}");
					}
				}
			}
			return base.CanRightClick(item);
		}
		public override void NetSend(Item item, BinaryWriter writer) {
			writer.Write(ammoLock);
			writer.Write(locked);
		}
		public override void NetReceive(Item item, BinaryReader reader) {
			ammoLock = reader.ReadInt32();
			locked = reader.ReadBoolean();
		}

		public override void ModifyTooltips(Item item,List<TooltipLine> tooltips) {
			if (ammoLock > 0) {
				if (ammoLock > ItemLoader.ItemCount) {
					ammoLock = 0;
					return;
				}
				Item i = new Item();
				i.SetDefaults(ammoLock);
				string a = "Prioritized";
				if (locked) {a = "Locked";}
				tooltips.Add(new TooltipLine(mod, "ammoLock", $"Ammo {a} to[i:{ammoLock}]{i.Name}\nShift and Right Click To Remove This"));
			}
		}
		
	}
	public class Hacc
	{
		public static void Add() {
			On.Terraria.Player.HasAmmo += HaccHasAmmo;
			On.Terraria.Player.PickAmmo += HaccPickAmmo;
		}

		static bool HaccHasAmmo(On.Terraria.Player.orig_HasAmmo orig,Player self,Item item, bool canUse) {
			if (item.GetGlobalItem<AmmoItem>().ammoLock > 0 && item.GetGlobalItem<AmmoItem>().locked) {
				for (int i = 0; i < 58; i++){
					if (self.inventory[i].type == item.GetGlobalItem<AmmoItem>().ammoLock && self.inventory[i].stack > 0){return true;}
				}
				return false;
			}
			return orig(self,item,canUse);
		}
		static void HaccPickAmmo(On.Terraria.Player.orig_PickAmmo orig,Player self,Item item, ref int shoot, ref float speed, ref bool canShoot, ref int Damage, ref float KnockBack, bool dontConsume) {
			if (item.GetGlobalItem<AmmoItem>().ammoLock > 0) {
				NewPickAmmo(self,item, ref shoot, ref speed, ref canShoot, ref Damage, ref KnockBack, dontConsume);
				return;
			}
			orig(self,item, ref shoot, ref speed, ref canShoot, ref Damage, ref KnockBack, dontConsume);
		}
		// Imagine using IL edit  - chad detour enjoyer
		//Copied from vanilla and changed a bit
		static void NewPickAmmo(Player player,Item weapon, ref int shoot, ref float speed, ref bool canShoot, ref int Damage, ref float KnockBack, bool dontConsume = false)
		{
			Item item = new Item();
			int ammo = weapon.GetGlobalItem<AmmoItem>().ammoLock;
			bool locked = weapon.GetGlobalItem<AmmoItem>().locked;
			bool flag = false;
			if (locked) {
				for (int i = 54; i < 58; i++){
					if (player.inventory[i].type == ammo && player.inventory[i].stack > 0){
						item = player.inventory[i];
						canShoot = true;
						flag = true;
						break;
					}
				}
				if (!flag){
					for (int j = 0; j < 54; j++) {
						if (player.inventory[j].type == ammo && player.inventory[j].stack > 0){
							item = player.inventory[j];
							canShoot = true;
							break;
						}
					}
				}
			}
			else {
				int firstIndex = -1;
				for (int i = 54; i < 58; i++)
				{
					if (player.inventory[i].type == ammo && player.inventory[i].stack > 0){
						item = player.inventory[i];
						canShoot = true;
						flag = true;
						break;
					}
					if (firstIndex == -1 && player.inventory[i].ammo == weapon.useAmmo && player.inventory[i].stack > 0)
					{
						firstIndex = i;
						canShoot = true;
					}
				}
				if (!flag)
				{
					for (int j = 0; j < 54; j++)
					{
						if (player.inventory[j].type == ammo && player.inventory[j].stack > 0){
							item = player.inventory[j];
							canShoot = true;
							flag = true;
							break;
						}
						if (firstIndex == -1 && player.inventory[j].ammo == weapon.useAmmo && player.inventory[j].stack > 0)
						{
							firstIndex = j;
							canShoot = true;
						}
					}
				}
				if (!flag && canShoot && firstIndex > -1) {
					item = player.inventory[firstIndex];
				}
			}
			if (!canShoot){return;}
			if (weapon.type == 1946)
			{
				shoot = 338 + item.type - 771;
				if (shoot > 341)
				{
					shoot = 341;
				}
			}
			else if (weapon.useAmmo == AmmoID.Rocket)
			{
				shoot += item.shoot;
			}
			else if (weapon.useAmmo == 780)
			{
				shoot += item.shoot;
			}
			else if (item.shoot > 0)
			{
				shoot = item.shoot;
			}
			if (weapon.type == 3019 && shoot == 1)
			{
				shoot = 485;
			}
			if (weapon.type == 3052)
			{
				shoot = 495;
			}
			if (weapon.type == 3245 && shoot == 21)
			{
				shoot = 532;
			}
			if (shoot == 42)
			{
				if (item.type == 370)
				{
					shoot = 65;
					Damage += 5;
				}
				else if (item.type == 408)
				{
					shoot = 68;
					Damage += 5;
				}
				else if (item.type == 1246)
				{
					shoot = 354;
					Damage += 5;
				}
			}
			if (player.inventory[player.selectedItem].type == 2888 && shoot == 1)
			{
				shoot = 469;
			}
			if (player.magicQuiver && (weapon.useAmmo == AmmoID.Arrow || weapon.useAmmo == AmmoID.Stake))
			{
				KnockBack = (int)((double)KnockBack * 1.1);
				speed *= 1.1f;
			}
			speed += item.shootSpeed;
			if (item.ranged)
			{
				if (item.damage > 0)
				{
					if (weapon.damage > 0)
					{
						Damage += (int)((float)(item.damage * Damage) / (float)weapon.damage);
					}
					else
					{
						Damage += item.damage;
					}
				}
			}
			else
			{
				Damage += item.damage;
			}
			if (weapon.useAmmo == AmmoID.Arrow && player.archery && speed < 20f)
			{
				speed *= 1.2f;
				if (speed > 20f)
				{
					speed = 20f;
				}
			}
			KnockBack += item.knockBack;
			ItemLoader.PickAmmo(weapon, item, player, ref shoot, ref speed, ref Damage, ref KnockBack);
			bool flag2 = dontConsume;
			if (weapon.type == 3245)
			{
				if (Main.rand.Next(3) == 0)
				{
					flag2 = true;
				}
				else if (player.thrownCost33 && Main.rand.Next(100) < 33)
				{
					flag2 = true;
				}
				else if (player.thrownCost50 && Main.rand.Next(100) < 50)
				{
					flag2 = true;
				}
			}
			if (weapon.type == 3475 && Main.rand.Next(3) != 0)
			{
				flag2 = true;
			}
			if (weapon.type == 3540 && Main.rand.Next(3) != 0)
			{
				flag2 = true;
			}
			if (player.magicQuiver && weapon.useAmmo == AmmoID.Arrow && Main.rand.Next(5) == 0)
			{
				flag2 = true;
			}
			if (player.ammoBox && Main.rand.Next(5) == 0)
			{
				flag2 = true;
			}
			if (player.ammoPotion && Main.rand.Next(5) == 0)
			{
				flag2 = true;
			}
			if (weapon.type == 1782 && Main.rand.Next(3) == 0)
			{
				flag2 = true;
			}
			if (weapon.type == 98 && Main.rand.Next(3) == 0)
			{
				flag2 = true;
			}
			if (weapon.type == 2270 && Main.rand.Next(2) == 0)
			{
				flag2 = true;
			}
			if (weapon.type == 533 && Main.rand.Next(2) == 0)
			{
				flag2 = true;
			}
			if (weapon.type == 1929 && Main.rand.Next(2) == 0)
			{
				flag2 = true;
			}
			if (weapon.type == 1553 && Main.rand.Next(2) == 0)
			{
				flag2 = true;
			}
			if (weapon.type == 434 && player.itemAnimation < PlayerHooks.TotalMeleeTime(weapon.useAnimation, player, weapon) - 2)
			{
				flag2 = true;
			}
			if (player.ammoCost80 && Main.rand.Next(5) == 0)
			{
				flag2 = true;
			}
			if (player.ammoCost75 && Main.rand.Next(4) == 0)
			{
				flag2 = true;
			}
			if (shoot == 85 && player.itemAnimation < player.itemAnimationMax - 6)
			{
				flag2 = true;
			}
			if ((shoot == 145 || shoot == 146 || shoot == 147 || shoot == 148 || shoot == 149) && player.itemAnimation < player.itemAnimationMax - 5)
			{
				flag2 = true;
			}
			if (!(flag2 | (!PlayerHooks.ConsumeAmmo(player, weapon, item) | !ItemLoader.ConsumeAmmo(weapon, item, player))) && item.consumable)
			{
				PlayerHooks.OnConsumeAmmo(player, weapon, item);
				ItemLoader.OnConsumeAmmo(weapon, item, player);
				item.stack--;
				if (item.stack <= 0)
				{
					item.active = false;
					item.TurnToAir();
				}
			}
		}
	}
}