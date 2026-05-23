using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class MatchResultsFormatter
{
    public static string BuildResultsText()
    {
        PlayerNetwork[] players = Object.FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);
        if (players.Length == 0)
            return "Match Results\nNo players found.";

        List<PlayerNetwork> sortedPlayers = new List<PlayerNetwork>(players);
        sortedPlayers.Sort((left, right) =>
        {
            int scoreCompare = right.Score.CompareTo(left.Score);
            if (scoreCompare != 0)
                return scoreCompare;

            int killsCompare = right.Kills.CompareTo(left.Kills);
            if (killsCompare != 0)
                return killsCompare;

            return left.Deaths.CompareTo(right.Deaths);
        });

        PlayerNetwork winner = sortedPlayers[0];
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Match Results");
        builder.AppendLine(
            $"Winner: {winner.Nickname}  (Score {winner.Score}, Kills {winner.Kills}, Deaths {winner.Deaths})");
        builder.AppendLine();

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            PlayerNetwork player = sortedPlayers[i];
            builder.AppendLine(
                $"{player.Nickname}  |  Score: {player.Score}  Kills: {player.Kills}  Deaths: {player.Deaths}");
        }

        return builder.ToString().TrimEnd();
    }
}
