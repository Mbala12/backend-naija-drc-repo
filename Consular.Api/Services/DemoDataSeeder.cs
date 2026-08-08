using Consular.Api.Data;
using Consular.Shared.Entities;
using Consular.Shared.Enums;
using Microsoft.AspNetCore.Identity;

namespace Consular.Api.Services;

public class DemoDataSeeder
{
    // Demo credentials for every seeded login-capable account (both Applicants and Users) —
    // this is the ONLY way to get into a freshly-seeded system, since Users can only otherwise
    // be created by an existing admin. Not meant for production use.
    public const string DemoPassword = "Demo1234!";

    private static readonly PasswordHasher<Applicant> ApplicantHasher = new();
    private static readonly PasswordHasher<User> UserHasher = new();

    public DemoSeedData BuildSeedData()
    {
        var regions = new List<RegionLookup>
        {
            new() { Code = "lagos", Valeur = 0, LibelleFr = "Lagos", LibelleEn = "Lagos", Ordre = 1, Actif = true },
            new() { Code = "abuja", Valeur = 1, LibelleFr = "Abuja", LibelleEn = "Abuja", Ordre = 2, Actif = true }
        };

        // Fixed catalog — one row per distinct authorization gate in the app (see
        // PermissionAuthorizationHandler). Never admin-created/deleted, only listed; what's
        // admin-configurable is which of these each Role below is given.
        var permissions = new List<Permission>
        {
            new() { Code = "Demandes.View", Label = "Voir la liste des dossiers" },
            new() { Code = "Demandes.Transition", Label = "Faire progresser un dossier" },
            new() { Code = "Admin.ViewData", Label = "Consulter les données d'administration" },
            new() { Code = "Admin.ManageData", Label = "Modifier/supprimer les données d'administration" },
            new() { Code = "Applicants.Lookup", Label = "Rechercher un postulant par email" },
            new() { Code = "Users.Manage", Label = "Gérer les comptes et les rôles" },
            new() { Code = "Reports.View", Label = "Consulter les rapports" }
        };
        Permission Perm(string code) => permissions.Single(p => p.Code == code);

        // Reproduces today's four real (Region, IsAdmin) combinations exactly, so behavior is
        // unchanged the moment this seeds — administrators are free to edit/add to these afterward.
        var roles = new List<Role>
        {
            new()
            {
                Name = "Agent de traitement",
                Description = "Traite les dossiers au quotidien : consultation et progression des demandes.",
                Permissions = new List<Permission> { Perm("Demandes.View"), Perm("Demandes.Transition"), Perm("Admin.ViewData"), Perm("Applicants.Lookup") }
            },
            new()
            {
                Name = "Consultation seule",
                Description = "Accès en lecture seule au tableau de bord des dossiers.",
                Permissions = new List<Permission> { Perm("Demandes.View") }
            },
            new()
            {
                Name = "Administrateur système",
                Description = "Accès complet : dossiers, données d'administration et gestion des comptes.",
                Permissions = new List<Permission>(permissions)
            },
            new()
            {
                Name = "Gestionnaire des comptes",
                Description = "Gère les comptes utilisateurs et les rôles, sans accès aux données d'administration.",
                Permissions = new List<Permission> { Perm("Demandes.View"), Perm("Users.Manage") }
            }
        };

        // Base document list shared within a category (Visa / EtatCivil) — each sub-type below
        // reuses it as its own copy since TypeService.DocumentsRequis* isn't a shared reference,
        // while the type-*specific* extra documents live in the frontend's visaInfo/acteInfo
        // translations (VisaInfoPage/ActeInfoPage tabs), not here.
        var visaDocumentsFr = new List<string>
        {
            "Passeport valide (au moins 6 mois de validité)",
            "Photo d'identité récente (fond blanc)",
            "Preuve d'hébergement à destination",
            "Billet d'avion aller-retour",
            "Preuve de moyens financiers suffisants (relevé bancaire)"
        };
        var visaDocumentsEn = new List<string>
        {
            "Valid passport (at least 6 months remaining)",
            "Recent passport-style photo (white background)",
            "Proof of accommodation at destination",
            "Round-trip flight ticket",
            "Proof of sufficient funds (bank statement)"
        };
        var acteDocumentsFr = new List<string>
        {
            "Pièce d'identité ou passeport d'un parent",
            "Justificatif du lien de parenté (livret de famille, acte de mariage si applicable)",
            "Déclaration de naissance ou ancien acte à réémettre"
        };
        var acteDocumentsEn = new List<string>
        {
            "ID card or passport of a parent",
            "Proof of relationship (family record book, marriage certificate if applicable)",
            "Birth declaration or previous certificate to be reissued"
        };

        // Each visa/acte sub-type used to be a single free-text value (Demande{Visa,EtatCivil}.
        // Type{Visa,Acte}) chosen within one shared TypeService per category. It's now its own
        // TypeService — its own Code (and NumeroReferenceGenerator prefix), its own MontantFrais,
        // editable independently in the admin dashboard — so each sub-type can actually be priced
        // and billed on its own instead of every sub-type under a category sharing one fee.
        var typeServices = new List<TypeService>
        {
            new()
            {
                Code = "VISA_TOURISTIQUE", Libelle = "Visa touristique", Categorie = TypeServiceCategorie.Visa,
                MontantFrais = 25000m, Description = "Visa de tourisme",
                DocumentsRequisFr = new List<string>(visaDocumentsFr), DocumentsRequisEn = new List<string>(visaDocumentsEn)
            },
            new()
            {
                Code = "VISA_AFFAIRES", Libelle = "Visa d'affaires", Categorie = TypeServiceCategorie.Visa,
                MontantFrais = 35000m, Description = "Visa pour voyage d'affaires",
                DocumentsRequisFr = new List<string>(visaDocumentsFr), DocumentsRequisEn = new List<string>(visaDocumentsEn)
            },
            new()
            {
                Code = "VISA_ETUDIANT", Libelle = "Visa étudiant", Categorie = TypeServiceCategorie.Visa,
                MontantFrais = 20000m, Description = "Visa pour études en RDC",
                DocumentsRequisFr = new List<string>(visaDocumentsFr), DocumentsRequisEn = new List<string>(visaDocumentsEn)
            },
            new()
            {
                Code = "VISA_TRANSIT", Libelle = "Visa de transit", Categorie = TypeServiceCategorie.Visa,
                MontantFrais = 15000m, Description = "Visa pour simple transit",
                DocumentsRequisFr = new List<string>(visaDocumentsFr), DocumentsRequisEn = new List<string>(visaDocumentsEn)
            },
            new()
            {
                Code = "VISA_TRAVAIL", Libelle = "Visa de travail", Categorie = TypeServiceCategorie.Visa,
                MontantFrais = 40000m, Description = "Visa pour occuper un emploi en RDC",
                DocumentsRequisFr = new List<string>(visaDocumentsFr), DocumentsRequisEn = new List<string>(visaDocumentsEn)
            },
            new()
            {
                // Diplomatic visas are conventionally fee-exempt — matches the note already shown
                // on VisaInfoPage's Diplomatique tab.
                Code = "VISA_DIPLOMATIQUE", Libelle = "Visa diplomatique", Categorie = TypeServiceCategorie.Visa,
                MontantFrais = 0m, Description = "Visa pour mission officielle",
                DocumentsRequisFr = new List<string>(visaDocumentsFr), DocumentsRequisEn = new List<string>(visaDocumentsEn)
            },
            new()
            {
                Code = "ACTE_NAISSANCE", Libelle = "Acte de naissance", Categorie = TypeServiceCategorie.EtatCivil,
                MontantFrais = 15000m, Description = "Extrait de naissance",
                DocumentsRequisFr = new List<string>(acteDocumentsFr), DocumentsRequisEn = new List<string>(acteDocumentsEn)
            },
            new()
            {
                Code = "ACTE_MARIAGE", Libelle = "Acte de mariage", Categorie = TypeServiceCategorie.EtatCivil,
                MontantFrais = 18000m, Description = "Extrait de mariage",
                DocumentsRequisFr = new List<string>(acteDocumentsFr), DocumentsRequisEn = new List<string>(acteDocumentsEn)
            },
            new()
            {
                Code = "ACTE_DECES", Libelle = "Acte de décès", Categorie = TypeServiceCategorie.EtatCivil,
                MontantFrais = 18000m, Description = "Extrait de décès",
                DocumentsRequisFr = new List<string>(acteDocumentsFr), DocumentsRequisEn = new List<string>(acteDocumentsEn)
            },
            new()
            {
                Code = "PASSPORT_RENEWAL", Libelle = "Passeport", Categorie = TypeServiceCategorie.Passeport,
                MontantFrais = 35000m, Description = "Service de passeport",
                DocumentsRequisFr = new List<string>
                {
                    "Passeport actuel ou expiré",
                    "Photo d'identité récente (fond blanc)",
                    "Justificatif de résidence ou d'immatriculation consulaire",
                    "Copie de la page biographique de l'ancien passeport"
                },
                DocumentsRequisEn = new List<string>
                {
                    "Current or expired passport",
                    "Recent passport-style photo (white background)",
                    "Proof of residency or consular registration",
                    "Copy of the biographic page of the old passport"
                }
            }
        };

        var statuts = new List<Statut>
        {
            // Libelle is the server's source-of-truth French text (see TypeService for the same
            // convention) — the frontend's translateCode() only ever substitutes an English
            // override on top of it, so an English Libelle here shows up untranslated in French.
            new() { Code = "SUBMITTED", Libelle = "Soumis", Ordre = 1, EstFinal = false, Actif = true },
            new() { Code = "UNDER_REVIEW", Libelle = "En cours d'examen", Ordre = 3, EstFinal = false, Actif = true },
            // Passeport-only sub-flow (see DemandeWorkflowRules) — reached only via the dashboard's
            // "Waiting biometrics"/"Collected biometrics" buttons on a Passeport-category demande.
            // Ordre sits right after UNDER_REVIEW so a passport case's happy path reads as strictly
            // increasing; TrackRequestPage filters these two out of the timeline entirely for every
            // other category so they never show up as a confusing "skipped" step there.
            new() { Code = "WAITING_BIOMETRICS", Libelle = "En attente de biométrie", Ordre = 4, EstFinal = false, Actif = true },
            new() { Code = "COLLECTED_BIOMETRICS", Libelle = "Biométrie collectée", Ordre = 5, EstFinal = false, Actif = true },
            new() { Code = "MISSING_DOCUMENTS", Libelle = "Documents manquants", Ordre = 6, EstFinal = false, Actif = true },
            new() { Code = "DOCUMENTS_RECEIVED", Libelle = "Documents reçus", Ordre = 7, EstFinal = false, Actif = true },
            new() { Code = "APPEAL_REVIEW", Libelle = "Recours en cours d'examen", Ordre = 8, EstFinal = false, Actif = true },
            new() { Code = "APPROVED", Libelle = "Approuvé", Ordre = 9, EstFinal = false, Actif = true },
            // REJECTED isn't EstFinal even though nothing further happens automatically — the
            // applicant can still appeal it within the deadline (see WorkingDays), so it isn't a
            // dead end the way COLLECTED is.
            new() { Code = "REJECTED", Libelle = "Rejeté", Ordre = 10, EstFinal = false, Actif = true },
            new() { Code = "COLLECTED", Libelle = "Collecté", Ordre = 11, EstFinal = true, Actif = true }
        };

        // Amina and Chinedu can log in and track their own requests (real hash — unlike the
        // placeholder this used to use, DemoPassword actually works). Fatima and Tunde are
        // beneficiary-only: someone submitted a request on their behalf and they've never
        // registered, so they have no password and can't log in yet.
        var applicants = new List<Applicant>
        {
            new() { Nom = "Amina Bello", Email = "amina.bello@example.com", Telephone = "+2348010000001", Nationalite = "Nigerian", CreatedAt = DateTime.UtcNow.AddDays(-14) },
            new() { Nom = "Chinedu Okafor", Email = "chinedu.okafor@example.com", Telephone = "+2348020000002", Nationalite = "Nigerian", CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { Nom = "Fatima Yusuf", Email = "fatima.yusuf@example.com", Telephone = "+2348030000003", Nationalite = "Nigerian", CreatedAt = DateTime.UtcNow.AddDays(-7) },
            new() { Nom = "Tunde Adebayo", Email = "tunde.adebayo@example.com", Telephone = "+2348040000004", Nationalite = "Nigerian", CreatedAt = DateTime.UtcNow.AddDays(-3) }
        };
        applicants[0].MotDePasseHash = ApplicantHasher.HashPassword(applicants[0], DemoPassword);
        applicants[1].MotDePasseHash = ApplicantHasher.HashPassword(applicants[1], DemoPassword);

        // The only accounts that can access the staff dashboard out of the box. "Admin Nord" is
        // the bootstrap admin — the sole way to create further Users (and Roles) on a fresh
        // system. RoleId is deliberately NOT set here — Roles aren't persisted yet at this point
        // in BuildSeedData, so Seed() wires it up itself once it has real, persisted Role ids
        // (same reason Demandes below resolve TypeServiceId/StatutId from a re-queried list
        // rather than from these in-memory objects).
        var users = new List<User>
        {
            new() { Nom = "Admin Nord", Email = "admin@embassy.local", Region = Region.North, CreatedAt = DateTime.UtcNow.AddDays(-30) },
            new() { Nom = "Agent Sud", Email = "agent.south@embassy.local", Region = Region.South, CreatedAt = DateTime.UtcNow.AddDays(-20) }
        };
        users[0].MotDePasseHash = UserHasher.HashPassword(users[0], DemoPassword);
        users[1].MotDePasseHash = UserHasher.HashPassword(users[1], DemoPassword);

        var demandes = new List<Demande>
        {
            CreateDemande("DEM-2026-000101", applicants[0], typeServices[0], statuts[2], "web", "Visa touristique pour Lagos", DateTime.UtcNow.AddDays(-6)),
            CreateDemande("DEM-2026-000102", applicants[1], typeServices[1], statuts[4], "mobile", "Acte de naissance pour enfant", DateTime.UtcNow.AddDays(-4)),
            CreateDemande("DEM-2026-000103", applicants[2], typeServices[2], statuts[5], "counter", "Renouvellement passeport", DateTime.UtcNow.AddDays(-2)),
            CreateDemande("DEM-2026-000104", applicants[3], typeServices[0], statuts[6], "web", "Visa d'affaires", DateTime.UtcNow.AddDays(-1))
        };

        // Default weekly appointment calendar so submission has something to book against out of
        // the box, for every category: weekdays, a morning block and an afternoon block (with a
        // lunch gap), capacity 5, symmetric across both regions/embassy locations. Each category
        // gets its own independent capacity pool (a Passeport slot and a Visa slot at the same
        // region/day/time are different resources).
        var appointmentSlotTemplates = new List<AppointmentSlotTemplate>();
        foreach (var categorie in new[] { TypeServiceCategorie.Visa, TypeServiceCategorie.EtatCivil, TypeServiceCategorie.Passeport })
        {
            foreach (var region in new[] { Region.North, Region.South })
            {
                foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
                {
                    foreach (var hour in new[] { 9, 10, 11, 14, 15 })
                    {
                        appointmentSlotTemplates.Add(new AppointmentSlotTemplate
                        {
                            Region = region,
                            Categorie = categorie,
                            DayOfWeek = day,
                            StartTime = new TimeOnly(hour, 0),
                            CapaciteMax = 5,
                            Actif = true
                        });
                    }
                }
            }
        }

        return new DemoSeedData(regions, permissions, roles, typeServices, statuts, applicants, users, demandes, appointmentSlotTemplates);
    }

    public void Seed(AppDbContext db)
    {
        var data = BuildSeedData();

        foreach (var region in data.Regions)
        {
            if (!db.RegionLookups.Any(r => r.Code == region.Code))
            {
                db.RegionLookups.Add(region);
            }
        }

        foreach (var permission in data.Permissions)
        {
            if (!db.Permissions.Any(p => p.Code == permission.Code))
            {
                db.Permissions.Add(permission);
            }
        }

        db.SaveChanges();

        // Unlike Statuts/TypeServices above (checked per-row by Code, so a new one added later
        // still reaches an already-seeded database), Roles are seeded only as a one-time
        // bootstrap: once any Role exists, administrators own this table — they may have already
        // renamed/deleted/repurposed a default role, and a per-name existence check would
        // resurrect one they deliberately removed.
        if (!db.Roles.Any())
        {
            // Re-point each Role's Permissions at the now-persisted rows (matched by Code) rather
            // than the in-memory objects BuildSeedData built them against, so EF updates the join
            // table instead of trying to INSERT duplicate Permission rows.
            var persistedPermissions = db.Permissions.ToDictionary(p => p.Code);
            foreach (var role in data.Roles)
            {
                role.Permissions = role.Permissions.Select(p => persistedPermissions[p.Code]).ToList();
                db.Roles.Add(role);
            }

            db.SaveChanges();
        }

        foreach (var service in data.TypeServices)
        {
            if (!db.TypeServices.Any(t => t.Code == service.Code))
            {
                db.TypeServices.Add(service);
            }
        }

        foreach (var statut in data.Statuts)
        {
            if (!db.Statuts.Any(s => s.Code == statut.Code))
            {
                db.Statuts.Add(statut);
            }
        }

        foreach (var slot in data.AppointmentSlotTemplates)
        {
            var exists = db.AppointmentSlotTemplates.Any(t => t.Region == slot.Region && t.Categorie == slot.Categorie && t.DayOfWeek == slot.DayOfWeek && t.StartTime == slot.StartTime);
            if (!exists)
            {
                db.AppointmentSlotTemplates.Add(slot);
            }
        }

        db.SaveChanges();

        foreach (var applicant in data.Applicants)
        {
            if (!db.Applicants.Any(a => a.Email == applicant.Email))
            {
                db.Applicants.Add(applicant);
            }
        }

        // "Admin Nord"/"Agent Sud" (data.Users[0]/[1]) were built in BuildSeedData before Roles
        // existed in the database, so RoleId couldn't be set there — wire it up now from the
        // persisted rows, by the same Name each is seeded with above.
        var persistedRoles = db.Roles.ToDictionary(r => r.Name);
        data.Users[0].RoleId = persistedRoles["Administrateur système"].Id;
        data.Users[1].RoleId = persistedRoles["Consultation seule"].Id;

        foreach (var user in data.Users)
        {
            if (!db.Users.Any(u => u.Email == user.Email))
            {
                db.Users.Add(user);
            }
        }

        db.SaveChanges();

        if (!db.Demandes.Any())
        {
            var persistedApplicants = db.Applicants.ToList();
            var persistedTypeServices = db.TypeServices.ToList();
            var persistedStatuts = db.Statuts.ToList();

            // Path is the full sequence of statuses this demande actually passed through, from
            // SUBMITTED to its current one — used below to generate a real DemandeHistorique
            // row per transition, so the tracking page's audit trail has a genuine date for
            // every step instead of showing "—" for demo data that never went through
            // DemandesController.Transition.
            var seedSpecs = new[]
            {
                new DemandSeedSpec("DEM-2026-000101", "amina.bello@example.com", "VISA_TOURISTIQUE",
                    new[] { "SUBMITTED", "UNDER_REVIEW" }, "web", "Visa touristique pour Lagos", DateTime.UtcNow.AddDays(-6), "Lagos"),
                new DemandSeedSpec("DEM-2026-000102", "chinedu.okafor@example.com", "ACTE_NAISSANCE",
                    new[] { "SUBMITTED", "UNDER_REVIEW", "APPROVED" }, "mobile", "Acte de naissance pour enfant", DateTime.UtcNow.AddDays(-4), "Lagos"),
                new DemandSeedSpec("DEM-2026-000103", "fatima.yusuf@example.com", "PASSPORT_RENEWAL",
                    new[] { "SUBMITTED", "UNDER_REVIEW", "REJECTED" }, "counter", "Renouvellement passeport", DateTime.UtcNow.AddDays(-2), "Lagos"),
                new DemandSeedSpec("DEM-2026-000104", "tunde.adebayo@example.com", "VISA_AFFAIRES",
                    new[] { "SUBMITTED", "UNDER_REVIEW", "APPROVED", "COLLECTED" }, "web", "Visa d'affaires", DateTime.UtcNow.AddDays(-1), "Lagos")
            };

            var demandesToAdd = new List<Demande>();
            var historiqueToAdd = new List<DemandeHistorique>();

            // Real Users.Nom values (not a placeholder) so ReportAggregationService.
            // BuildStaffActivityAsync — which semi-joins DemandeHistoriques.ActorName against
            // Users.Nom — actually counts this seeded activity instead of silently dropping it,
            // leaving the Reports tab's Staff Activity section empty despite real transitions.
            var seededStaffNames = new[] { "Admin Nord", "Agent Sud" };

            for (var specIndex = 0; specIndex < seedSpecs.Length; specIndex++)
            {
                var spec = seedSpecs[specIndex];
                var actorName = seededStaffNames[specIndex % seededStaffNames.Length];
                var applicant = persistedApplicants.SingleOrDefault(a => a.Email == spec.ApplicantEmail)
                    ?? throw new InvalidOperationException($"Applicant for demande {spec.NumeroReference} was not found");
                var typeService = persistedTypeServices.SingleOrDefault(t => t.Code == spec.ServiceCode)
                    ?? throw new InvalidOperationException($"Service for demande {spec.NumeroReference} was not found");
                var finalStatutCode = spec.Path[^1];
                var finalStatut = persistedStatuts.SingleOrDefault(s => s.Code == finalStatutCode)
                    ?? throw new InvalidOperationException($"Status for demande {spec.NumeroReference} was not found");

                // Each step in the path lands a few hours after the previous one; the last
                // step's timestamp becomes the demande's UpdatedAt, matching how a real
                // transition updates it.
                var stepTimestamps = Enumerable.Range(0, spec.Path.Length)
                    .Select(i => spec.DateDepot.AddHours(6 * i))
                    .ToArray();

                var demande = new Demande
                {
                    NumeroReference = spec.NumeroReference,
                    ApplicantId = applicant.Id,
                    SoumisParApplicantId = applicant.MotDePasseHash is null ? null : applicant.Id,
                    TypeServiceId = typeService.Id,
                    StatutId = finalStatut.Id,
                    CanalDepot = spec.CanalDepot,
                    EquipeAssignee = spec.EquipeAssignee,
                    NoteDocumentsManquantes = finalStatutCode == "MISSING_DOCUMENTS" ? spec.NoteDocumentsManquantes : null,
                    DateDepot = spec.DateDepot,
                    UpdatedAt = stepTimestamps[^1],
                    Attributs = null
                };
                demandesToAdd.Add(demande);

                for (var i = 1; i < spec.Path.Length; i++)
                {
                    var origine = persistedStatuts.SingleOrDefault(s => s.Code == spec.Path[i - 1])
                        ?? throw new InvalidOperationException($"Status '{spec.Path[i - 1]}' is not seeded.");
                    var destination = persistedStatuts.SingleOrDefault(s => s.Code == spec.Path[i])
                        ?? throw new InvalidOperationException($"Status '{spec.Path[i]}' is not seeded.");

                    historiqueToAdd.Add(new DemandeHistorique
                    {
                        DemandeId = demande.Id,
                        StatutOrigineId = origine.Id,
                        StatutDestinationId = destination.Id,
                        ActorName = actorName,
                        DateChangement = stepTimestamps[i]
                    });
                }
            }

            db.Demandes.AddRange(demandesToAdd);
            db.DemandeHistoriques.AddRange(historiqueToAdd);
            db.SaveChanges();
        }
    }

    private sealed record DemandSeedSpec(
        string NumeroReference,
        string ApplicantEmail,
        string ServiceCode,
        string[] Path,
        string CanalDepot,
        string NoteDocumentsManquantes,
        DateTime DateDepot,
        string EquipeAssignee);

    private static Demande CreateDemande(string numeroReference, Applicant applicant, TypeService typeService, Statut statut, string canalDepot, string note, DateTime dateDepot)
    {
        return new Demande
        {
            NumeroReference = numeroReference,
            ApplicantId = applicant.Id,
            SoumisParApplicantId = applicant.MotDePasseHash is null ? null : applicant.Id,
            TypeServiceId = typeService.Id,
            StatutId = statut.Id,
            CanalDepot = canalDepot,
            EquipeAssignee = "Lagos",
            NoteDocumentsManquantes = statut.Code == "MISSING_DOCUMENTS" ? note : null,
            DateDepot = dateDepot,
            UpdatedAt = dateDepot.AddHours(2),
            Attributs = null
        };
    }
}

public class DemoSeedData
{
    public DemoSeedData(List<RegionLookup> regions, List<Permission> permissions, List<Role> roles, List<TypeService> typeServices, List<Statut> statuts, List<Applicant> applicants, List<User> users, List<Demande> demandes, List<AppointmentSlotTemplate> appointmentSlotTemplates)
    {
        Regions = regions;
        Permissions = permissions;
        Roles = roles;
        TypeServices = typeServices;
        Statuts = statuts;
        Applicants = applicants;
        Users = users;
        Demandes = demandes;
        AppointmentSlotTemplates = appointmentSlotTemplates;
    }

    public List<RegionLookup> Regions { get; }
    public List<Permission> Permissions { get; }
    public List<Role> Roles { get; }
    public List<TypeService> TypeServices { get; }
    public List<Statut> Statuts { get; }
    public List<Applicant> Applicants { get; }
    public List<User> Users { get; }
    public List<Demande> Demandes { get; }
    public List<AppointmentSlotTemplate> AppointmentSlotTemplates { get; }
}
