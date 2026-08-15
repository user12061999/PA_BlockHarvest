using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager ins;
    public AudioSource sound;
    public AudioSource music;

    public AudioClip loseSound;
    public AudioClip winSound;
    public AudioClip bgSound;

    [Header("Sound Effects")]
    public AudioClip placeBlockSound;       // se_put
    public AudioClip invalidDropSound;      // se_ng
    public AudioClip pickupBlockSound;       // se_tap (MỚI)
    public AudioClip spawnBlocksSound;      // se_create_block (MỚI)
    public AudioClip resourceGainSound;     // se_growth / se_increase
    public AudioClip splashSound;           // se_splash (MỚI)
    public AudioClip dirt3x3Sound;          // se_upgrade (MỚI)
    public AudioClip animalEatSound;        // se_eat
    public AudioClip clearBoardSound;       // se_harvest
    public AudioClip tileBreakSound;        // se_icon_jump
    public AudioClip resourceFlySound;      // se_launch_seed
    public AudioClip goalHitSound;          // se_drop_gold
    public AudioClip goalCompletedSound;    // se_perfect_filled (MỚI)
    public AudioClip basketPickupSound;     // se_get_prize
    public AudioClip truckMoveSound;        // se_car
    public AudioClip buttonClickSound;      // se_decide / se_ok (MỚI)

    private void Awake()
    {
        ins = this;
        if (sound == null)
        {
            sound = GetComponent<AudioSource>();
        }

        if (music == null)
        {
            music = sound;
        }
    }

    private void Start()
    {
        if (LunaManager.ins != null && LunaManager.ins.bgMusic != null)
        {
            bgSound = LunaManager.ins.bgMusic;
        }

        PlayMusic();
    }

    public void PlaySound(AudioClip audioClip)
    {
        if (audioClip == null || sound == null)
        {
            return;
        }

        sound.PlayOneShot(audioClip, 1);
    }

    public void PlayPlaceBlock() { PlaySound(placeBlockSound); }
    public void PlayInvalidDrop() { PlaySound(invalidDropSound); }
    public void PlayPickupBlock() { PlaySound(pickupBlockSound); }
    public void PlaySpawnBlocks() { PlaySound(spawnBlocksSound); }
    public void PlayResourceGain() { PlaySound(resourceGainSound); }
    public void PlaySplash() { PlaySound(splashSound); }
    public void PlayDirt3x3() { PlaySound(dirt3x3Sound); }
    public void PlayAnimalEat() { PlaySound(animalEatSound); }
    public void PlayClearBoard() { PlaySound(clearBoardSound); }
    public void PlayTileBreak() { PlaySound(tileBreakSound != null ? tileBreakSound : clearBoardSound); }
    public void PlayResourceFly() { PlaySound(resourceFlySound); }
    public void PlayGoalHit() { PlaySound(goalHitSound != null ? goalHitSound : resourceGainSound); }
    public void PlayGoalCompleted() { PlaySound(goalCompletedSound); }
    public void PlayBasketPickup() { PlaySound(basketPickupSound); }
    public void PlayTruckMove() { PlaySound(truckMoveSound); }
    public void PlayButtonClick() { PlaySound(buttonClickSound); }

    public void PlayMusicLose()
    {
        if (music == null || loseSound == null) return;
        music.Stop();
        music.loop = false;
        music.clip = loseSound;
        music.Play();
    }

    public void PlayMusicWin()
    {
        if (music == null || winSound == null) return;
        music.Stop();
        music.loop = false;
        music.clip = winSound;
        music.Play();
    }

    public void PlayMusic()
    {
        if (music == null || bgSound == null) return;
        music.Stop();
        music.loop = true;
        music.clip = bgSound;
        music.Play();
    }
}