﻿using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// tracks player's typing
public class TrackTyping : MonoBehaviour {
	public Text typed_text; // shows typed text
	public Text entry_ok; // displays 'OK' or 'NO' if player can type or not
	public Dictionary<char, Image> key_map; // map from characters to key images
	public Transform keyboard; // keyboard transform (holds key images)
	public GameObject key_prefab; // prefab for key image object

	string buffer; // contains typed text
	int count; // number of characters typed
	string[] rows = { "qwertyuiop", "asdfghjkl", "zxcvbnm", " " };
	float[] row_offsets = { 0f, 24f, 72f, 0 };

	void Start () {
		typed_text.text = "";
		key_map = new Dictionary<char, Image> ();
		buffer = "";
		count = 0;
		createKeyboard ();
		// initialize key colors to gray
		foreach (KeyValuePair<char, Image> pair in key_map)
			pair.Value.color = Color.gray;
	}

	void Update () {
		if (BattleManager.main.pause || BattleManager.main.enabled == false) {
			entry_ok.text = "NO";
			return;
		} else entry_ok.text = "OK";
		// check key presses
		if (Input.GetKeyDown (KeyCode.Return)) {
			Debug.Log ("Player casts " + buffer.ToUpper().Replace(' ', '-'));
			AudioPlayer.main.playSFX (0, SFXType.UI, "sfx_enter");
			BattleManager.main.attackCurrent (buffer); // attack currently targeted enemy
			buffer = "";
			count = 0;
		} else if (Input.GetKey (KeyCode.Backspace)) {
			if (Input.GetKeyDown (KeyCode.Backspace)) {
				if (count > 0) {
					buffer = buffer.Remove (count - 1, 1);
					count = count - 1;
				}
			}
		} else {
			string in_str = Input.inputString;
			buffer += in_str;
			count += in_str.Length;
			// highlight pressed keys
			foreach (char c in in_str) 
				StartCoroutine(colorKey(c));
		}
		// update display
		typed_text.text = buffer.Replace(" ", "-").ToUpper();
	}

	// create visual keyboard keys
	void createKeyboard() {
		for (int i = 0; i < rows.Length; i++) {
			for (int j = 0; j < rows[i].Length; j++) {
				GameObject new_key = GameObject.Instantiate (key_prefab);
				new_key.transform.SetParent (keyboard);
				new_key.transform.localScale = new Vector3 (1, 1, 1);
				new_key.transform.localPosition = new Vector3 (row_offsets[i] + j*48, i*-48, 0);
				new_key.GetComponentInChildren<Text> ().text = rows [i] [j].ToString();
				key_map.Add (rows [i] [j], new_key.GetComponent<Image> ());
			}
		}
	}

	// light up key for a short period of time
	IEnumerator colorKey(char c) {
		if (key_map.ContainsKey (c)) {
			key_map [c].color = Color.white;
			yield return new WaitForSeconds (0.1f);
			key_map [c].color = Color.gray;
		}
	}
}
