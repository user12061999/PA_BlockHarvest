using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class AudioManager : MonoBehaviour
{
    public static AudioManager ins;
    public AudioSource sound;
    public AudioSource music;

    public AudioClip loseSound;
    public AudioClip winSound;
    public AudioClip bgSound;
    [Header("Sound Effects")]
    public AudioClip placeBlockSound;
    public AudioClip invalidDropSound;
    public AudioClip resourceGainSound;
    public AudioClip animalEatSound;
    public AudioClip clearBoardSound;
    public AudioClip resourceFlySound;
    public AudioClip basketPickupSound;
    public AudioClip truckMoveSound;

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
    public void PlayResourceGain() { PlaySound(resourceGainSound); }
    public void PlayAnimalEat() { PlaySound(animalEatSound); }
    public void PlayClearBoard() { PlaySound(clearBoardSound); }
    public void PlayResourceFly() { PlaySound(resourceFlySound); }
    public void PlayBasketPickup() { PlaySound(basketPickupSound); }
    public void PlayTruckMove() { PlaySound(truckMoveSound); }

    public void PlayMusicLose()
    {
        if (music == null || loseSound == null)
        {
            return;
        }

        music.Stop();
        music.loop = false;
        music.clip = loseSound;
        music.Play();
    }
    public void PlayMusicWin()
    {
        if (music == null || winSound == null)
        {
            return;
        }

        music.Stop();
        music.loop = false;
        music.clip = winSound;
        music.Play();
    }
    public void PlayMusic()
    {
        if (music == null || bgSound == null)
        {
            return;
        }

        music.Stop();
        music.loop = true;
        music.clip = bgSound;
        music.Play();
    }
}
