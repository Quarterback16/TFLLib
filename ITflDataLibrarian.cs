using System;
using System.Data;
using System.Runtime.InteropServices;
using NLog;

namespace TFLLib
{
   public interface ITflDataLibrarian
   {
      string NflConnectionName { get; set; }
      string NflConnectionString { get; set; }
      Logger Logger { get; set; }
      string GetCurrentWeek();
      string GetCurrentSeason();
      DataSet PositionsUsed(string teamCode, string season);
      string GetStarter(string teamCode, string season, int week, string pos, int slot);
      DataSet GetLineup(string teamCode, string season, int week);
      DataSet GetStarts(string playerCode, string season);
      DataRow TeamDataFor(string teamCode, string season);
      string TeamFor(string teamCode, string season);
      string GetDivFor(string teamCode, string season);
      string GetRatingsFor(string teamCode, string season);

      /// <summary>
      ///   Returns teams in XML
      /// </summary>
      /// <param name="season"></param>
      /// <returns></returns>
      string Teams(string season);

      DataSet TeamRecord(string teamCode, string season);
      int GetTeamStat(string teamCode, string statName, string season);

      /// <summary>
      ///   Returns teams in a DataSet
      /// </summary>
      /// <param name="season"></param>
      /// <returns></returns>
      DataSet TeamsDs(string season);

      /// <summary>
      ///   Returns teams in a Dataset for an optional Division
      /// </summary>
      /// <param name="season"></param>
      /// <param name="div"></param>
      /// <returns></returns>
      DataSet GetTeams(string season, [Optional] string div);

      DataSet ScoresDs(string season, [Optional] string week, [Optional] string game);
      DataSet ScoresDs(string scoreType, string teamCode, string season, string week, string game);
      int CountScoresByType(string season, string scoreType);
      DataTable ScoresDtByType(string season, string scoreType);

      /// <summary>
      ///    Get a data set of scores for a particular player.
      /// </summary>
      /// <param name="scoreType">What type of score, eg FG</param>
      /// <param name="playerId">ID of the player who scored</param>
      /// <returns>DataSet</returns>
      DataSet GetScoresFor(string scoreType, string playerId);

      DataSet GetSeasonScoresFor(string scoreType, string season1, string season2);
      DataSet GetPlayersScoring(string scoreType, string season1, string season2, string scoreSlot);

      /// <summary>
      ///    Get a data set of scores for a particular team in a particular game.
      /// </summary>
      /// <param name="scoreType">What type of score, eg FG</param>
      /// <param name="teamCode">ID of the team who scored</param>
      /// <param name="season">the season</param>
      /// <param name="week">the week</param>
      /// <param name="gameCode">the game</param>
      /// <returns>DataSet</returns>
      DataSet GetTeamScoresFor(string scoreType, string teamCode, string season, string week, string gameCode);

      int GetTotTeamScoresFor(string scoreType, string teamCode, string season, string week, string gameCode);
      DataSet GetTeamScoresFor(string scoreType, string teamCode, string season);
      DataSet GetTeamRegularSeasonScoresFor(string scoreType, string teamCode, string season);
      DataSet GetTeamDefensiveScoresFor(string teamCode, string season, string week, string gameCode);

      /// <summary>
      ///   Looking for overtime games
      /// </summary>
      /// <param name="season"></param>
      /// <param name="week"></param>
      /// <param name="gameCode"></param>
      /// <returns></returns>
      DataSet GetOvertimeScoresFor(string season, string week, string gameCode);

      DataSet GetScoresForWeek(string scoreType, string playerId, string season, int week);
      DataSet GetScoresForWeeks(string scoreType, string playerId, string season, int fromWeek, int toWeek, string id);
      DataSet PlayerScoresDs(string season, string week, string playerId);
      DataSet PenaltyScores(string season, string week, string teamCode);
      decimal TeamScores(string scoreCode, string season, string week, string game, string teamCode);
      bool AnyScoresForGame(string playerId, string season, int week, string gameCode);
      DataSet PlayerStatsDs(string season, string week, [Optional] string playerId);
      int GetStat(string statCode, string season, string week, string game);
      DataSet GetStats(string statCode, string teamCode, string season, string week, string game);
      decimal GetTotStats(string teamCode, string statCode, string season, string week, string game);
      decimal TeamStats(string statCode, string season, string week, string game, string teamCode);
      string PlayerStats(string statCode, string season, string week, string game, string teamCode);
      string PlayerStats(string statCode, string season, string week, string playerId);
      DataSet GetStatsForWeeks(string playerId, string season, int fromWeek, int toWeek, string statType);
      bool AnyStatsForGame(string playerId, string season, int week, string gameCode);
      string NextId(string firstName, string surname);
      DataSet GetReturners();
      DataSet GetCurrentScoring(string categoryIn);
      DataSet GetScoring(string categoryIn, bool currentOnly, int season);
      DataSet GetFreeAgents(string categoryIn, bool currentOnly, int season);
      DataSet GetPlayer(string teamCode, string strCat, string strRole, string strPos);
      DataSet GetPlayer(string teamCode, string strRole, string strPos);
      DataSet GetTeamPlayers(string teamCode, string strCat);
      DataSet GetPlayer(string playerCode);

      DataSet GetPlayer( string firstName, string lastName );

      string GetPlayerName(string playerCode);

      /// <summary>
      ///   Returns a collection of players
      /// </summary>
      /// <param name="strCat">Pass * for all categories</param>
      /// <param name="sPos"></param>
      /// <param name="role"></param>
      /// <returns></returns>
      DataSet GetPlayers(string strCat, [Optional] string sPos, [Optional] string role, [Optional] string rookieYr);

      DataSet GetCurrentPlayers(string teamCode, [Optional] string strCat, [Optional] string sPos, [Optional] string role);
      DataSet GetOffensivePlayers(string strCats);
      DataSet GetPlayersByRole(string strCat, [Optional] string sRole, [Optional] string sPos);
      DataTable GetDistinctPositions();
      DataTable GetDistinctColleges();
      bool PlayerExists(string firstname, string surname, string college, [Optional] string dob);
      string GetStatus(string playerCode, string compCode, string season);
      DataSet GetFTeamsDs(string season, string compCode);
      DataRow GetFTeamDr(string season, string compCode, string ownerCode);
      DateTime GetSeasonStartDate(string season);
      DataSet MovesDs(string teamCode, DateTime dFrom, DateTime dTo);
      string PlayedFor(string playerId, int season, int week);

      /// <summary>
      ///   Returns when the player was drafted by looking at his
      ///   team contracts.
      /// </summary>
      /// <param name="playerCode"></param>
      /// <returns></returns>
      string Drafted(string playerCode);

      /// <summary>
      /// Checks to see if the specified player has Retired.
      ///
      /// </summary>
      /// <param name="playerCode">The player code.</param>
      /// <returns>The season in which the player retired.</returns>
      string Retired(string playerCode);

      DataSet ResultFor(string teamCode, int season, int week);
      DataSet GameFor(string season, string week, string gameNo);
      DataSet SchedDs(string season, string week);

      /// <summary>
      ///    Returns a dataset containing a teams schedule for a particular season, in week order.
      /// </summary>
      /// <param name="season">eg 2005</param>
      /// <param name="teamCode">eg SF</param>
      /// <returns>DataSet "sched"</returns>
      DataSet TeamSchedDs(string season, string teamCode);

      DataSet GetSeason(string sTeam, string sSeason);
      string GetSuperbowlWinner(string season);
      DataSet GetGames(string season, string week);
      DataSet GetGames(int seasonIn, int weekIn);
      DataSet GetSeason(string seasonIn, string startWeek, string endWeek);
      DataSet GetSeason(string seasonIn);
      DataTable GetSeasonDt(string seasonIn);
      DataSet GetAllGames();
      DataSet GetAllGames(int season);
      DataSet GetAllGames(string teamCode);
      DataSet GetAllGames(string teamCode, string season);
      DataSet GetAllRegularSeasonGames(string teamCode, string season);
      DataTable GetAllGamesDt(string teamCode);
      DataTable GetAllGames(string teamCode, DateTime since);
      DataRow GetGameByCode(string season, string week, string gameCode);
      DataRow GetGame(string season, string week, string teamCode);
      string GetWeekFor(DateTime when);
      DataRow GetWeekRecord(DateTime when);
      DataRow GetNflWeekFor(DateTime when);
      string GetGameCode(string season, string week, string teamCode);
      DataRow GetGame(string season, string week, string teamCode, string gameCode);
      DataRow GetGameAfter(string teamCode, DateTime when);
      DataRow GetGamePriorTo(string teamCode, DateTime when);
      DataSet GetGamesBetween(string teamCodeOne, string teamCodeTwo, DateTime since);
      DataSet GetLastGame(string teamCode1, string teamCode2);
      DataSet GetLastGames(string teamCode, int nGames, int nSeason);
      DataSet GetLastGames(string teamCode, int nGames, DateTime theDate);
      DataSet GetLastRegularSeasonGames(string teamCode, int nGames, DateTime theDate);
      DataSet GetAllPredictions(string season, string method);
      DataSet GetPrediction(string method, string season, string week, string gameCode);
      DataSet GetPrediction(string method, string season, string week);

      void InsertPrediction(string method, string season, string week, string gameCode,
         int homeScore, int awayScore, int htdp, int htdr, int htdd, int htds, int hfg,
         int atdp, int atdr, int atdd, int atds, int afg,
         int hydp, int hydr, int aydp, int aydr
         );

      void UpdatePrediction(string method, string season, string week, string gameCode, int homeScore, int awayScore,
         int htdp, int htdr, int htdd, int htds, int hfg,
         int atdp, int atdr, int atdd, int atds, int afg,
         int hydp, int hydr, int aydp, int aydr
         );

      DataSet GetAce( string season, string week, string playerId );
      void DeleteTeamAces(string teamCode, string season, int week );

      void UpdateAce( string season, string week, string playerId, string playerCat, string teamCode,
         decimal load, int touches
         );

      void InsertAce( string season, string week, string teamCode, string playerId, string playerCat,
         decimal load, int touches
         );

      DataSet GetUnitRatings(DateTime when);
      string GetUnitRatings(DateTime when, string teamCode);
      void SaveUnitRatings(string ratings, DateTime when, string teamCode);
      void InsertRun(string stepName, TimeSpan ts, string category);
      DateTime GetLastRun(string reportName);
      DataTable GetRuns(DateTime sinceDate);

      void InsertUnitPerformance(string teamCode, string unitCode, string season, int week,
         string opponent, string leader, string oppLeader, string unitRate, string oppRate,
         int yds, int tds, int ints, decimal sacks);

      DataRow GetUnitPerformance(string teamCode, string season, int week, string unitCode);
      void DeleteUnitPerformance(string teamCode, string season, int week, string unitCode);
      void StorePlayerRoleAndPos(string role, string pos, string playerId);

      void StoreResult(string season, string week, string gameNo, int awayScore, int homeScore,
         int homeTdp, int awayTdp, int homeTdr, int awayTdr, int homeFg, int awayFg,
         int awayTdd, int homeTdd, int awayTds, int homeTds);

      void StorePlayer(string playerId, string firstname, string surname, string teamCode,
         string role, int heightFeet, int heightInches, int weight, string college, string rookieYr,
         string posdesc, string category, string dob);

      DataSet GetPlayerGameMetrics(string playerCode, string gameCode);

      void InsertPlayerGameMetric(string playerId, string gameCode,
         int projYDp, int YDp, int projYDr, int ydr,
         int projTDp, int TDp, decimal projTDr, int tdr,
         int projTDc, int TDc, int projYDc, int YDc,
         int projFG, int fg, int projPat, int pat, decimal fpts
         );

      string UpdatePlayerGameMetric(string playerId, string gameCode,
         int projYDp, int YDp, int projYDr, int ydr,
         int projTDp, int TDp, decimal projTDr, int tdr,
         int projTDc, int TDc, int projYDc, int YDc,
         int projFG, int fg, int projPat, int pat
         );

      DataSet GetAllPlayerGameMetrics(string season, string week);
      DataSet GetAllPlayerGameMetrics(string season);
      DataSet GetAllPlayerGameMetricsForPlayer(string season, string playerCode);
      void ClearPlayerGameMetrics(string gameKey);
      void WriteToLog(string msg);

      void CloseServitude( DateTime closeDate, string playerId );
      void RetirePlayer( DateTime closeDate, string playerId );
      void SetCurrentTeam( string playerId, string teamCode );
      void SetRole( string playerId, string roleCode );

      void Sign( string playerId, string teamCode, DateTime when, string how );
   }
}