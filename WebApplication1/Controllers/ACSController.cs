using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ACSController : ControllerBase
    {
        private readonly ILogger<ACSController> _logger;
        private readonly string _filePath;

        public ACSController(ILogger<ACSController> logger)
        {
            _logger = logger;
            _filePath = @"C:\DataAvaya\dailyReport_new.txt";
        }

        // ==================== PARSING FILE ====================

        private (List<SkillSummary> skillSummary, List<Agent> agents, List<AgentAuxTime> auxTimes) ParseFile()
        {
            if (!System.IO.File.Exists(_filePath))
            {
                throw new FileNotFoundException($"File tidak ditemukan: {_filePath}");
            }

            var lines = System.IO.File.ReadAllLines(_filePath)
                                     .Where(l => !string.IsNullOrWhiteSpace(l))
                                     .ToList();

            var skillSummary = new List<SkillSummary>();
            var agents = new List<Agent>();
            var auxTimes = new List<AgentAuxTime>();

            // Parse Skill Summary
            for (int i = 1; i <= 2 && i < lines.Count; i++)
            {
                var parts = lines[i].Split(';');
                if (parts.Length >= 11)
                {
                    skillSummary.Add(new SkillSummary
                    {
                        Skill = parts[0],
                        SkillState = parts[1],
                        AgentsStaffed = ParseInt(parts[2]),
                        AgentsAvail = ParseInt(parts[3]),
                        AgentsInACW = ParseInt(parts[4]),
                        AgentsInAUX = ParseInt(parts[5]),
                        AgentsOnACDCalls = ParseInt(parts[6]),
                        AgentsInOther = ParseInt(parts[7]),
                        CallOffered = ParseInt(parts[8]),
                        AbandonCalls = ParseInt(parts[9]),
                        AnswerCalls = ParseInt(parts[10])
                    });
                }
            }

            // Cari posisi section
            int agentStartIndex = -1;
            int auxStartIndex = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains("Agent Name;Login ID;Extn;Direction;State;AUX Reason;Time;Split/Skill;ACD Calls"))
                {
                    agentStartIndex = i + 1;
                }
                if (lines[i].Contains("Agent Name;Extn;Avail Time"))
                {
                    auxStartIndex = i + 1;
                }
            }

            // Parse Agents dengan ACD Calls
            if (agentStartIndex > 0)
            {
                for (int i = agentStartIndex; i < lines.Count; i++)
                {
                    if (i >= lines.Count || lines[i].StartsWith("Agent Name;Extn;Avail Time"))
                        break;

                    var parts = lines[i].Split(';');
                    if (parts.Length >= 9 && !string.IsNullOrEmpty(parts[0]) && parts[0] != ";;")
                    {
                        if (parts[0] == ";;;" || parts[0].Contains("UNKNOWN"))
                            continue;

                        agents.Add(new Agent
                        {
                            AgentName = parts[0],
                            LoginId = parts[1],
                            Extension = parts[2],
                            Direction = parts[3],
                            State = parts[4],
                            AuxReason = parts[5],
                            Time = parts[6],
                            Skill = parts[7],
                            ACDCalls = ParseInt(parts[8])
                        });
                    }
                }
            }

            // Parse AUX Time (ambil unique)
            if (auxStartIndex > 0)
            {
                var processedAux = new HashSet<string>();
                for (int i = auxStartIndex; i < lines.Count; i++)
                {
                    if (i >= lines.Count || string.IsNullOrWhiteSpace(lines[i]))
                        continue;

                    var parts = lines[i].Split(';');
                    if (parts.Length >= 12 && !string.IsNullOrEmpty(parts[0]) && parts[0] != ";;" && parts[0] != "Agent Name")
                    {
                        if (parts[0].StartsWith(";;"))
                            continue;

                        var key = $"{parts[0]}_{parts[1]}";
                        if (processedAux.Contains(key))
                            continue;

                        if (parts[2] != ":00:00" && parts[2] != "00:00:00")
                        {
                            processedAux.Add(key);
                            auxTimes.Add(new AgentAuxTime
                            {
                                AgentName = parts[0],
                                Extension = parts[1],
                                AvailTime = parts[2],
                                System = parts[3],
                                PraySholat = parts[4],
                                LunchDinner = parts[5],
                                ShortBreak = parts[6],
                                TeamMeeting = parts[7],
                                Training = parts[8],
                                FridayPray = parts[9],
                                FollowUpCustomer = parts[10],
                                Toilet = parts.Length > 11 ? parts[11] : ""
                            });
                        }
                    }
                }
            }

            return (skillSummary, agents, auxTimes);
        }

        private int ParseInt(string value)
        {
            return int.TryParse(value, out int result) ? result : 0;
        }

        // ==================== ENDPOINT ====================

        /// <summary>
        /// Get FULL DATA dengan ACD Calls di agentDetail
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var (skillSummary, agents, auxTimes) = await Task.Run(() => ParseFile());

                // Agent Detail dengan ACD Calls
                var agentDetail = agents.Select(a => new
                {
                    a.AgentName,
                    a.LoginId,
                    a.Extension,
                    a.Direction,
                    a.State,
                    a.AuxReason,
                    a.Time,
                    a.Skill,
                    a.ACDCalls  // ← ACD Calls ditambahkan
                }).ToList();

                // Response dengan format yang diminta
                var response = new
                {
                    skillSummary = skillSummary,
                    agentDetail = agentDetail,
                    agentAuxDetail = auxTimes,
                    lastUpdate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff+07:00")
                };

                return Ok(response);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { error = ex.Message, path = _filePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting data");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    // ==================== MODELS ====================

    public class SkillSummary
    {
        public string Skill { get; set; } = string.Empty;
        public string SkillState { get; set; } = string.Empty;
        public int AgentsStaffed { get; set; }
        public int AgentsAvail { get; set; }
        public int AgentsInACW { get; set; }
        public int AgentsInAUX { get; set; }
        public int AgentsOnACDCalls { get; set; }
        public int AgentsInOther { get; set; }
        public int CallOffered { get; set; }
        public int AbandonCalls { get; set; }
        public int AnswerCalls { get; set; }
    }

    public class Agent
    {
        public string AgentName { get; set; } = string.Empty;
        public string LoginId { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string AuxReason { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Skill { get; set; } = string.Empty;
        public int ACDCalls { get; set; }
    }

    public class AgentAuxTime
    {
        public string AgentName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string AvailTime { get; set; } = string.Empty;
        public string System { get; set; } = string.Empty;
        public string PraySholat { get; set; } = string.Empty;
        public string LunchDinner { get; set; } = string.Empty;
        public string ShortBreak { get; set; } = string.Empty;
        public string TeamMeeting { get; set; } = string.Empty;
        public string Training { get; set; } = string.Empty;
        public string FridayPray { get; set; } = string.Empty;
        public string FollowUpCustomer { get; set; } = string.Empty;
        public string Toilet { get; set; } = string.Empty;
    }
}