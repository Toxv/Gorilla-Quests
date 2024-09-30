using System;

[Serializable]
public class Quest
{
    public string Name { get; private set; }
    public float Duration { get; private set; }
    public int Progress { get; private set; }
    public int Reward { get; private set; }
    public Quest(string name, float duration, int reward)
    {
        Name = name;
        Duration = duration;
        Progress = 0;
        Reward = reward;
    }
    public void IncrementProgress()
    {
        if (Progress < Duration)
        {
            Progress++;
        }
    }
    public bool IsComplete()
    {
        return Progress >= Duration;
    }
    public void ResetProgress()
    {
        Progress = 0;
    }
    public override string ToString()
    {
        return $"{Name} - Progress: {Progress}/{Duration} minutes, Reward: {Reward} points";
    }
}