using System;

// Anything that can tell the engine "the player just released a shot".
public interface IShotDetector {
    event Action Shot;
}
