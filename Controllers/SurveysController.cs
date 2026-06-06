using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Priser.Data;
using Priser.Models.Entities;
using Priser.Services;

namespace Priser.Controllers;

[Authorize]
public class SurveysController(AppDbContext db, WalletService wallets, CurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var tenantId = currentUser.TenantId!.Value;
        var userId = currentUser.UserId!.Value;

        var now = DateTime.UtcNow;
        var surveys = await db.Surveys
            .Where(s => s.TenantId == tenantId && s.IsActive && (s.EndsAt == null || s.EndsAt > now))
            .ToListAsync();

        var answered = await db.SurveyResponses
            .Where(r => r.UserId == userId)
            .Select(r => r.SurveyId)
            .ToListAsync();

        ViewBag.Surveys = surveys;
        ViewBag.Answered = answered;
        return View();
    }

    public async Task<IActionResult> Take(Guid id)
    {
        var tenantId = currentUser.TenantId!.Value;
        var userId = currentUser.UserId!.Value;

        var survey = await db.Surveys
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.IsActive);

        if (survey == null) return NotFound();

        var alreadyAnswered = await db.SurveyResponses.AnyAsync(r => r.SurveyId == id && r.UserId == userId);
        if (alreadyAnswered)
        {
            TempData["Info"] = "Você já respondeu esta pesquisa.";
            return RedirectToAction("Index");
        }

        ViewBag.Survey = survey;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid surveyId, Dictionary<Guid, string> answers)
    {
        var tenantId = currentUser.TenantId!.Value;
        var userId = currentUser.UserId!.Value;

        var survey = await db.Surveys.Include(s => s.Questions).FirstOrDefaultAsync(s => s.Id == surveyId);
        if (survey == null) return NotFound();

        var alreadyAnswered = await db.SurveyResponses.AnyAsync(r => r.SurveyId == surveyId && r.UserId == userId);
        if (alreadyAnswered) return RedirectToAction("Index");

        var response = new SurveyResponse { SurveyId = surveyId, UserId = userId };
        db.SurveyResponses.Add(response);
        await db.SaveChangesAsync();

        foreach (var (questionId, value) in answers)
        {
            db.SurveyAnswers.Add(new SurveyAnswer
            {
                SurveyResponseId = response.Id,
                QuestionId = questionId,
                Value = value
            });
        }

        if (survey.RewardPoints.HasValue && survey.RewardPoints > 0)
        {
            response.Rewarded = true;
            await wallets.CreditPointsAsync(userId, tenantId, survey.RewardPoints.Value,
                "survey_reward", $"Recompensa por participar: {survey.Title}");
        }

        await db.SaveChangesAsync();
        TempData["Success"] = survey.RewardPoints > 0
            ? $"Obrigado! Você ganhou {survey.RewardPoints} pontos por participar! 🎉"
            : "Obrigado por participar!";

        return RedirectToAction("Index");
    }
}
