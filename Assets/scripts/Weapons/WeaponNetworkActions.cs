﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Weapons;
using PlayGroup;
using Sprites;

public class WeaponNetworkActions : NetworkBehaviour {

	private GameObject spritesObj;
	private PlayerSprites playerSprites;
	private PlayerMove playerMove;
	private SoundNetworkActions soundNetworkActions;
	private GameObject bloodSplatPrefab;

	//Lerp parameters
	private float lerpProgress = 0f;
	private bool lerping = false;
	private Vector3 lerpFrom;
	private Vector3 lerpTo;
	private float speed = 7f;
	private bool isForLerpBack = false;

	void Start(){
		spritesObj = transform.Find("Sprites").gameObject;
		playerSprites = GetComponent<PlayerSprites>();
		playerMove = GetComponent<PlayerMove>();
		soundNetworkActions = GetComponent<SoundNetworkActions>();
		bloodSplatPrefab = Resources.Load("BloodSplat") as GameObject;
	}

	[Command]
	public void CmdLoadMagazine(GameObject weapon, GameObject magazine){
		Weapon_Ballistic w = weapon.GetComponent<Weapon_Ballistic>();
		NetworkInstanceId nID = magazine.GetComponent<NetworkIdentity>().netId;
		w.magNetID = nID;
	}

	[Command]
	public void CmdUnloadWeapon(GameObject weapon){
		Weapon_Ballistic w = weapon.GetComponent<Weapon_Ballistic>();
		NetworkInstanceId newID = NetworkInstanceId.Invalid;
		w.magNetID = newID;
	}

	[Command]
	public void CmdShootBullet(Vector2 direction, string bulletName){
		if (!playerMove.allowInput)
			return;
		
		GameObject bullet = GameObject.Instantiate(Resources.Load(bulletName) as GameObject,transform.position, Quaternion.identity);
		var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
		BulletBehaviour b = bullet.GetComponent<BulletBehaviour>();
		b.Shoot(direction, angle, gameObject.name);

		//This is used to determine where bullet shot should head towards on client
		Ray2D ray = new Ray2D(transform.position, direction);
		RpcShootBullet(ray.GetPoint(30f), bulletName);

		//TODO add a check to see if bullet or energy weapon
		//BulletCasings
		GameObject casing = GameObject.Instantiate(Resources.Load("BulletCasing") as GameObject, transform.position, Quaternion.identity);
		NetworkServer.Spawn(casing);
	}

	//Bullets are just graphical candy on the client, give them the end point and let 
	//them work out the start pos and direction
	[ClientRpc]
	void RpcShootBullet(Vector2 endPos, string bulletName){
		if (!playerMove.allowInput)
			return;
		//TODO adjust the sound using the bulletName
		SoundManager.PlayAtPosition("ShootSMG", transform.position);

		if (CustomNetworkManager.Instance._isServer)
			return;
		
		GameObject bullet = GameObject.Instantiate(Resources.Load(bulletName) as GameObject,transform.position, Quaternion.identity);
		Vector2 playerPos = new Vector2(transform.position.x, transform.position.y);
		Vector2 dir = (endPos - playerPos).normalized;
		var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
		BulletBehaviour b = bullet.GetComponent<BulletBehaviour>();
		b.Shoot(dir, angle, gameObject.name);
	}



	[Command]
	public void CmdKnifeAttackMob(GameObject npcObj, Vector2 stabDirection)
	{
		if (!playerMove.allowInput)
			return;
		
		Living attackTarget = npcObj.GetComponent<Living>();
		if (npcObj != gameObject) {
			RpcKnifeAttackLerp(stabDirection);
		}
		attackTarget.RpcReceiveDamage(gameObject.name);
		BloodSplat(npcObj.transform.position,BloodSplatSize.medium);
		soundNetworkActions.RpcPlayNetworkSound("BladeSlice", transform.position);
	}

	[Command]
	public void CmdKnifeHarvestMob(GameObject npcObj, Vector2 stabDirection)
	{
		if (!playerMove.allowInput)
			return;
		
		Living attackTarget = npcObj.GetComponent<Living>();
		RpcKnifeAttackLerp(stabDirection);
		attackTarget.HarvestIt();
		soundNetworkActions.RpcPlayNetworkSound("BladeSlice", transform.position);
	}

	[ClientRpc]
	void RpcKnifeAttackLerp(Vector2 stabDir){
		if (lerping)
			return;
		
		if (PlayerManager.LocalPlayer.name == gameObject.name) {
			PlayerManager.LocalPlayerScript.hitIcon.ShowHitIcon(stabDir);
			PlayerManager.LocalPlayerScript.playerMove.allowInput = false;
		}
		lerpFrom = transform.position;
		Vector3 newDir = stabDir * 0.5f;
		newDir.z = lerpFrom.z;
		lerpTo = lerpFrom + newDir;
		lerpProgress = 0f;
		isForLerpBack = true;
		lerping = true;
	}

	[Server]
	public void BloodSplat(Vector3 pos,BloodSplatSize splatSize){
		GameObject b = GameObject.Instantiate(bloodSplatPrefab, pos, Quaternion.identity);
		NetworkServer.Spawn(b);
		BloodSplat bSplat = b.GetComponent<BloodSplat>();
		bSplat.SplatBlood(splatSize);
	}

	//Server lerps
	void Update(){
		if (lerping) {
			lerpProgress += Time.deltaTime;
			spritesObj.transform.position = Vector3.Lerp(lerpFrom, lerpTo, lerpProgress * speed);
			if (spritesObj.transform.position == lerpTo || lerpProgress > 2f) {
				if (!isForLerpBack) {
					ResetLerp();
					spritesObj.transform.localPosition = Vector3.zero;
					if (PlayerManager.LocalPlayer.name == gameObject.name) {
						PlayerManager.LocalPlayerScript.playerMove.allowInput = true;
					}
				} else {
					//To lerp back from knife attack
					ResetLerp();
					lerpTo = lerpFrom;
					lerpFrom = spritesObj.transform.position;
					lerping = true;
				}
			}
		}
	}
	void ResetLerp(){
		lerpProgress = 0f;
		lerping = false;
		isForLerpBack = false;
	}


}