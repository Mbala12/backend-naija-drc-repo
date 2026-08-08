namespace Consular.Api.Services;

// Pure state machine for Demande transitions: given the current Statut code and a requested
// action, resolves the next Statut code. Actor authorization (who is allowed to call this at
// all) is enforced separately at the controller level — staff actions require the "North" role
// (DemandesController.Transition), while "appeal" and "submit-documents" are applicant
// self-service and reachable anonymously (DemandesController.Appeal /
// SubmitMissingDocuments), each with its own 10-working-day deadline check (see WorkingDays).
// Which region owns the dossier next (EquipeAssignee) is no longer decided here — it's stamped
// by DemandesController from the acting staff member's own Region, since the region that
// handles a dossier is a fact about who touched it, not about the transition itself.
public static class DemandeWorkflowRules
{
    // COLLECTED is the only true dead end — once a case is picked up there's nothing left to do.
    // REJECTED is deliberately NOT here even though it looks terminal: the applicant can still
    // appeal it (within the deadline), so it needs an outgoing transition.
    private static readonly HashSet<string> FinalStatuts = new(StringComparer.OrdinalIgnoreCase)
    {
        "COLLECTED"
    };

    private static readonly Dictionary<(string Statut, string Action), string> Transitions =
        new(new TupleComparer())
        {
            [("SUBMITTED", "begin-review")] = "UNDER_REVIEW",
            [("SUBMITTED", "reject")] = "REJECTED",
            [("UNDER_REVIEW", "approve")] = "APPROVED",
            // Staff flagging missing documents, straight from the initial review — the dashboard
            // surfaces this as a single dynamic button (see DashboardPage.jsx) that reads
            // "Approve" (or, for a Passeport-category demande, "Waiting biometrics" — see below)
            // until a note is typed, then "Missing documents".
            [("UNDER_REVIEW", "mark-missing-documents")] = "MISSING_DOCUMENTS",
            // Passeport-only sub-flow: a passport demande doesn't go straight from UNDER_REVIEW to
            // APPROVED — it needs an in-person biometric capture first. The dashboard only exposes
            // "waiting-biometrics"/"collect-biometrics" as buttons for TypeServiceCategorie.Passeport
            // demandes (see DashboardPage.jsx's isPassportDemande), but the transitions themselves
            // are plain statut-code rules like everything else here — nothing category-aware lives
            // in this state machine. "reject" from UNDER_REVIEW is likewise only surfaced in the
            // dashboard for a passport demande (every other category still only offers
            // approve/missing-documents at this stage), reusing the same reason note field.
            [("UNDER_REVIEW", "reject")] = "REJECTED",
            [("UNDER_REVIEW", "waiting-biometrics")] = "WAITING_BIOMETRICS",
            [("WAITING_BIOMETRICS", "collect-biometrics")] = "COLLECTED_BIOMETRICS",
            [("COLLECTED_BIOMETRICS", "approve")] = "APPROVED",
            [("MISSING_DOCUMENTS", "reject")] = "REJECTED",
            // Applicant confirms they've attached the missing documents (see UploadDocument +
            // DemandesController.SubmitMissingDocuments) — this goes to DOCUMENTS_RECEIVED rather
            // than back to UNDER_REVIEW, since staff need to re-examine what's actually a
            // resubmission. Kept distinct from APPEAL_REVIEW (below) even though both funnel into
            // the same staff approve/reject decision — "documents received" and "appealing a
            // rejection" are different things to show the applicant and staff on the status badge.
            [("MISSING_DOCUMENTS", "submit-documents")] = "DOCUMENTS_RECEIVED",
            // Unlike the very first UNDER_REVIEW approval (which lands on APPROVED, awaiting a
            // separate "confirm collection" click), approving here goes straight to COLLECTED —
            // by this point the case has already been through a full re-review cycle, so there's
            // no separate pickup step left to track.
            [("DOCUMENTS_RECEIVED", "approve")] = "COLLECTED",
            [("DOCUMENTS_RECEIVED", "reject")] = "REJECTED",
            // Applicant-initiated appeal of a rejection (see DemandesController.Appeal).
            [("REJECTED", "appeal")] = "APPEAL_REVIEW",
            [("APPEAL_REVIEW", "approve")] = "COLLECTED",
            [("APPEAL_REVIEW", "reject")] = "REJECTED",
            [("APPROVED", "collect")] = "COLLECTED"
        };

    public static bool TryGetTransition(string currentStatutCode, string action, out string? nextStatutCode)
    {
        nextStatutCode = null;

        if (FinalStatuts.Contains(currentStatutCode))
        {
            return false;
        }

        if (Transitions.TryGetValue((currentStatutCode, action), out var next))
        {
            nextStatutCode = next;
            return true;
        }

        return false;
    }

    private class TupleComparer : IEqualityComparer<(string Statut, string Action)>
    {
        public bool Equals((string Statut, string Action) x, (string Statut, string Action) y) =>
            string.Equals(x.Statut, y.Statut, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Action, y.Action, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Statut, string Action) obj) =>
            HashCode.Combine(obj.Statut.ToUpperInvariant(), obj.Action.ToUpperInvariant());
    }
}
