﻿using Prospect.Unreal.Net.Actors;

namespace Prospect.Unreal.Runtime;

public class UPlayer : AActor
{
    public APlayerController? PlayerController { get; set; }
    
    public int CurrentNetSpeed { get; set; }
}