namespace HekaSomi.Lab.Core;

public static class ParticipationPilot
{
    // Original scenario prompts; a practice instrument, not a validated psychological scale.
    public static ParticipationTest Create() => new()
    {
        Id = "hekasomi-scenarios-pilot", Version = "1", Language = "en", Title = "Everyday choices · 12-question practice test",
        Items = [
            new("p01", "You join a meetup where you know nobody. What do you usually do first?", ["Start a conversation", "Listen before joining in", "Look for a shared activity"]),
            new("p02", "Your group has one free afternoon. How would you choose what to do?", ["Make a plan in advance", "Decide together on the day", "Leave time for something spontaneous"]),
            new("p03", "A friend asks you to explain a new tool. Where would you start?", ["A concrete example", "The overall idea", "A step-by-step demonstration"]),
            new("p04", "You disagree with a teammate's suggestion. What feels most natural?", ["State my concern directly", "Ask questions about their reasoning", "Take time to think before replying"]),
            new("p05", "You have to learn an unfamiliar game. What would you prefer?", ["Read the rules first", "Watch a round", "Learn while playing"]),
            new("p06", "A group conversation moves very quickly. What do you tend to do?", ["Jump in when I have a thought", "Wait for a clear opening", "Return to my point afterward"]),
            new("p07", "Your team finishes a difficult task. What would you most want to do next?", ["Review what could improve", "Celebrate with the group", "Take a quiet break"]),
            new("p08", "A new activity has an uncertain outcome. Which approach suits you?", ["Try a small experiment", "Gather more information first", "Ask someone to try it with me"]),
            new("p09", "You can give feedback on a project. What would you emphasize first?", ["What works well", "The most useful improvement", "Questions about the creator's goals"]),
            new("p10", "You have a free evening with no commitments. What appeals most?", ["Spend time with other people", "Work on a personal interest", "Rest without making a plan"]),
            new("p11", "A teammate is confused by your message. How would you clarify it?", ["Rewrite it more briefly", "Add an example", "Talk it through together"]),
            new("p12", "Your group must choose between two equally good ideas. What would you suggest?", ["Compare explicit criteria", "Take a group vote", "Try one and revisit later"])
        ]
    };
}
