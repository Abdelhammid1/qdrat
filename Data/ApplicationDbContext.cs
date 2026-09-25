using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QdratNew.Entities;
using QdratNew.Entities.Frontend;

namespace QdratNew.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }


    public DbSet<Branch> Branches { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Curriculum> Curriculums { get; set; }  // ✅ أضف هذا السطر
    public DbSet<Section> Sections { get; set; }
    public DbSet<Batch> Batches { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionOption> QuestionOptions { get; set; }
    public DbSet<StudentExamResult> StudentExamResults { get; set; }
    //public DbSet<QuestionAttempt> QuestionAttempts { get; set; }
    public DbSet<StudentWeaknessTraining> StudentWeaknessTrainings { get; set; }
    public DbSet<StudentReviewedMistake> StudentReviewedMistakes { get; set; }
    public DbSet<StudentPracticeAnswer> StudentPracticeAnswers { get; set; }
    public DbSet<ExamCurriculumQuestionCount> ExamCurriculumQuestionCounts { get; set; }
    public DbSet<SystemLog> SystemLogs { get; set; }
    public DbSet<CorporateRegistrationRequest> CorporateRegistrationRequests { get; set; }
    public DbSet<PerformanceIndicatorExamToBatch> PerformanceIndicatorExamToBatch { get; set; }
    public DbSet<PerformanceIndicatorExamQuestion> PerformanceIndicatorExamQuestions { get; set; }
    public DbSet<PerformanceIndicatorExamStudent> PerformanceIndicatorExamStudents { get; set; }
    public DbSet<RemedialSessionLog> RemedialSessionLogs { get; set; }
    public DbSet<RemedialVideo> RemedialVideos { get; set; }
    public DbSet<RemedialVideoQuestion> RemedialVideoQuestions { get; set; }
    public DbSet<RemedialQuizQuestion> RemedialQuizQuestions { get; set; }
    public DbSet<HeroSlide> HeroSlides { get; set; }
    public DbSet<QdratNew.Entities.Frontend.FrontendCourse> FrontendCourses { get; set; }
    public DbSet<QdratNew.Entities.Frontend.FrontendCourseSection> FrontendCourseSections { get; set; }
    public DbSet<QdratNew.Entities.Frontend.FrontendCourseRegistration> FrontendCourseRegistrations { get; set; }
    public DbSet<CorporateRegistrationSetting> CorporateRegistrationSettings { get; set; }
    public DbSet<Partner> Partners { get; set; }
    public DbSet<UserPartner> UserPartners { get; set; }
    public DbSet<PartnerSubscription> PartnerSubscriptions { get; set; }
    public DbSet<PartnerSubscriptionPeriod> PartnerSubscriptionPeriods { get; set; }
    public DbSet<PartnerSubscriptionCourse> PartnerSubscriptionCourses { get; set; }
    public DbSet<HomeworkDraft> HomeworkDrafts { get; set; }
    public DbSet<HomeworkDraftQuestion> HomeworkDraftQuestions { get; set; }
    public DbSet<StudentExamSnapshot> StudentExamSnapshots { get; set; }
    public DbSet<FrontendLead> FrontendLeads { get; set; }
    public DbSet<FrontendLeadCourse> FrontendLeadCourses { get; set; }
    public DbSet<InstructorBatchRole> InstructorBatchRoles { get; set; }
    public DbSet<InstructorBatchPermission> InstructorBatchPermissions { get; set; }
    public DbSet<EmployeeBatchAccess> EmployeeBatchAccesses { get; set; }
    public DbSet<ExamAssignmentToStudent> ExamAssignmentsToStudents { get; set; }
    public DbSet<ExamDraft> ExamDrafts { get; set; }
    public DbSet<ExamDraftQuestion> ExamDraftQuestions { get; set; }
    public DbSet<InstructorModel> InstructorModels { get; set; }
    public DbSet<InstructorModelQuestion> InstructorModelQuestions { get; set; }
    public DbSet<PromoExamSession> PromoExamSessions { get; set; }
    public DbSet<PromoExamAttempt> PromoExamAttempts { get; set; }
    public DbSet<PromoExamResult> PromoExamResults { get; set; }
    public DbSet<PromoLead> PromoLeads { get; set; }
    public DbSet<AdminPermissionProfile> AdminPermissionProfiles { get; set; }
    public DbSet<AdminPermissionModule> AdminPermissionModules { get; set; }
    public DbSet<AdminPermission> AdminPermissions { get; set; }
    public DbSet<AdminUserProfile> AdminUserProfiles { get; set; }
    public DbSet<StudentHomeworkAnalytics> StudentHomeworkAnalytics { get; set; }
    public DbSet<SuccessPartner> SuccessPartners { get; set; }
    public DbSet<DecisionRecommendation> DecisionRecommendations { get; set; }
    public DbSet<InterventionTask> InterventionTasks { get; set; }

    public DbSet<QuestionAttemptNew> QuestionAttemptNew { get; set; }
    public DbSet<ProfessionalModel> ProfessionalModels { get; set; }
    public DbSet<ProfessionalModelQuestion> ProfessionalModelQuestions { get; set; }
    public DbSet<CourseCurriculum> CourseCurriculums { get; set; }
    public DbSet<StudentRankHistory> StudentRankHistories { get; set; }
    public DbSet<ContactMessage> ContactMessages { get; set; }
    public DbSet<PlatformGoal> PlatformGoals { get; set; }

    // 🟢 نظام الصلاحيات الجديد
    public DbSet<InstructorCriticalQuestionTask> InstructorCriticalQuestionTasks { get; set; }
    public DbSet<InstructorCriticalQuestionTaskItem> InstructorCriticalQuestionTaskItems { get; set; }
    public DbSet<PerformanceIndicatorExam> PerformanceIndicatorExams { get; set; }
    public DbSet<PerformanceIndicatorExamSection> PerformanceIndicatorExamSections { get; set; }
    public DbSet<StudentIndicatorResult> StudentIndicatorResults { get; set; }
    public DbSet<IndicatorRemedialCorrelation> IndicatorRemedialCorrelations { get; set; }


    public DbSet<ProfessionalModelPartner> ProfessionalModelPartners { get; set; }
    public DbSet<ProfessionalModelSubscriptionPeriod> ProfessionalModelSubscriptionPeriods { get; set; }

    public DbSet<CurriculumInstructor> CurriculumInstructors { get; set; }
    public DbSet<StudySessionRating> StudySessionRatings { get; set; }
    public DbSet<InstituteWorkingHours> InstituteWorkingHours { get; set; }
    public DbSet<WorkSchedule> WorkSchedules { get; set; }
    public DbSet<InstructorWorkSchedule> InstructorWorkSchedules { get; set; }
    public DbSet<StudyRoom> StudyRooms { get; set; }
    public DbSet<StudentRemedialQuizResult> StudentRemedialQuizResults { get; set; }
    public DbSet<EnhancementSkillSet> EnhancementSkillSets { get; set; }
    public DbSet<EnhancementSetIndicator> EnhancementSetIndicators { get; set; }

    public DbSet<RemedialLesson> RemedialLessons { get; set; }
    public DbSet<RemedialQuiz> RemedialQuizzes { get; set; }

    public DbSet<QuestionAuditLog> QuestionAuditLogs { get; set; }
    public DbSet<HomeworkSet> HomeworkSets { get; set; }
    public DbSet<HomeworkArchiveAccess> HomeworkArchiveAccesses { get; set; }
    public DbSet<AttendanceBatchArchiveAccess> AttendanceBatchArchiveAccesses { get; set; }
    public DbSet<PlacementExamBatchArchiveAccess> PlacementExamBatchArchiveAccesses { get; set; }
    public DbSet<ExamAssignmentBatchArchiveAccess> ExamAssignmentBatchArchiveAccesses { get; set; }
    public DbSet<PerformanceIndicatorExamArchiveAccess> PerformanceIndicatorExamArchiveAccesses { get; set; }
    public DbSet<BatchesArchiveAccess> BatchesArchiveAccesses { get; set; }
    public DbSet<UserImpersonationAccess> UserImpersonationAccesses { get; set; }
    public DbSet<BatchInstructorGraduatedAccess> BatchInstructorGraduatedAccesses { get; set; }
    public DbSet<ExamAssignmentToBatch> ExamAssignmentsToBatches { get; set; }
    public DbSet<ExamStudentStatus> ExamStudentStatuses { get; set; }
    public DbSet<StudentAnswer> StudentAnswers { get; set; }
    public DbSet<StudentAIAnalysis> StudentAIAnalyses { get; set; }
    public DbSet<StudentWeakness> StudentWeaknesses { get; set; }
    public DbSet<LessonResource> LessonResources { get; set; }
    public DbSet<StudentPost> StudentPosts { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<SocialMediaLink> SocialMediaLinks { get; set; }
    public DbSet<AdminActivityLog> AdminActivityLogs { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<EnhancementSkillAssignment> EnhancementSkillAssignments { get; set; }
    public DbSet<EnhancementSkillSessionResult> EnhancementSkillSessionResults { get; set; }
    public DbSet<InstructorAIRecommendation> InstructorAIRecommendations { get; set; }
    public DbSet<StudentLessonCompletion> StudentLessonCompletions { get; set; }
    public DbSet<HomeworkSetSection> HomeworkSetSections { get; set; }
    public DbSet<HomeworkSetStudent> HomeworkSetStudents { get; set; }
    public DbSet<HomeworkSetAttempt> HomeworkSetAttempts { get; set; }

    public DbSet<HomeworkSessionLog> HomeworkSessionLogs { get; set; }
    public DbSet<HomeworkTimeTamperLog> HomeworkTimeTamperLogs { get; set; }
    public DbSet<SuspiciousActivity> SuspiciousActivities { get; set; }
    public DbSet<IntegrityViolationLog> IntegrityViolationLogs { get; set; }

    public DbSet<StudentActivityLog> StudentActivityLogs { get; set; }
    public DbSet<UserLoginLog> UserLoginLogs { get; set; }

    public DbSet<StudentPrediction> StudentPredictions { get; set; }
    public DbSet<EnhancementSkillResult> EnhancementSkillResults { get; set; }

    public DbSet<SectionUnit> SectionUnits { get; set; }

    // 🧠 داخل الكلاس ApplicationDbContext:
    public DbSet<Assignment> Assignments { get; set; }
    public DbSet<AssignmentQuestion> AssignmentQuestions { get; set; }
    public DbSet<BatchLessonCompletion> BatchLessonCompletions { get; set; }
    public DbSet<VerbalPassage> VerbalPassages { get; set; }

    public DbSet<MockExam> MockExams { get; set; }
    public DbSet<MockExamQuestion> MockExamQuestions { get; set; }
    public DbSet<StaticPageContent> StaticPageContents { get; set; }
    public DbSet<StaticPage> StaticPages { get; set; }

    public DbSet<EnhancementSkillSession> EnhancementSkillSessions { get; set; }
    public DbSet<PromoExperience> PromoExperiences { get; set; }

    public DbSet<Homework> Homeworks { get; set; }
    public DbSet<Lecture> Lecture { get; set; } // ✅ تأكد مفرد مش Lectures

    public DbSet<Unit> Units { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<Session> Sessions { get; set; }  // ✅ إضافة الجلسات الحضورية
    public DbSet<Course> Courses { get; set; }
    public DbSet<CourseInstructor> CourseInstructors { get; set; }
    public DbSet<Instructor> Instructors { get; set; }
    public DbSet<Parent> Parents { get; set; }
    public DbSet<StudentPerformance> StudentPerformances { get; set; }  // ✅ تمت إضافته هنا!
    public DbSet<Student> Students { get; set; }
    public DbSet<StudentCourse> StudentCourses { get; set; }
    public DbSet<RemedialPlanInteraction> RemedialPlanInteractions { get; set; }
    public DbSet<InstructorCurriculumBatch> InstructorCurriculumBatches { get; set; }

    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Challenge> Challenges { get; set; } // ✅ إضافة التحديات
    public DbSet<ChallengeParticipation> ChallengeParticipations { get; set; }
    public DbSet<RemedialPlan> RemedialPlans { get; set; }
    public DbSet<RemedialSession> RemedialSessions { get; set; }
    public DbSet<StudySessionReservation> StudySessionReservations { get; set; }
    public DbSet<StudentAchievement> StudentAchievements { get; set; }
    public DbSet<StudentProgress> StudentProgress { get; set; }
    public DbSet<StudyPlan> StudyPlans { get; set; }
    public DbSet<StudentCourseEnrollment> StudentCourseEnrollments { get; set; }
    public DbSet<ExamAssignment> ExamAssignments { get; set; }
    public DbSet<StudentBatchEnrollment> StudentBatchEnrollments { get; set; }
    public DbSet<QdratNew.Entities.Frontend.SubCourse> SubCourses { get; set; }


    public DbSet<AdminProfile> AdminProfiles { get; set; }
    public DbSet<AdminProfileControllerPermission> AdminProfileControllerPermissions { get; set; }
    public DbSet<AdminProfileCustomPermission> AdminProfileCustomPermissions { get; set; }

    public DbSet<Exam> Exams { get; set; }
    public DbSet<ExamQuestion> ExamQuestions { get; set; } // إن وجد لاحقًا
    public DbSet<ExamStudentQuestionOrder> ExamStudentQuestionOrders { get; set; }

    // Parent Portal
    public DbSet<ParentSmartPracticeRequest> ParentSmartPracticeRequests { get; set; }
    public DbSet<ParentStudentInsight> ParentStudentInsights { get; set; }
    public DbSet<ParentActionLog> ParentActionLogs { get; set; }
    public DbSet<ParentMessage> ParentMessages { get; set; }

    public DbSet<ProfessionalCertificateCourse> ProfessionalCertificateCourses { get; set; }
    public DbSet<ProfessionalCertificateRegistration> ProfessionalCertificateRegistrations { get; set; }
    public DbSet<ProfessionalCertificateSectionSetting> ProfessionalCertificateSectionSettings { get; set; }

    public DbSet<CourseCollection> CourseCollections { get; set; }
    public DbSet<CourseCollectionCourse> CourseCollectionCourses { get; set; }
    public DbSet<CourseCollectionSponsor> CourseCollectionSponsors { get; set; }
    public DbSet<CourseCollectionRegistration> CourseCollectionRegistrations { get; set; }

    public DbSet<MinistrySimExam> MinistrySimExams { get; set; }
    public DbSet<MinistrySimExamStage> MinistrySimExamStages { get; set; }
    public DbSet<MinistrySimExamStageIndicatorSelection> MinistrySimExamStageIndicatorSelections { get; set; }
    public DbSet<MinistrySimExamStageQuestion> MinistrySimExamStageQuestions { get; set; }
    public DbSet<MinistrySimExamAssignmentToBatch> MinistrySimExamAssignmentsToBatches { get; set; }
    public DbSet<MinistrySimExamAssignmentToStudent> MinistrySimExamAssignmentsToStudents { get; set; }
    public DbSet<MinistrySimExamGuestStudent> MinistrySimExamGuestStudents { get; set; }
    public DbSet<MinistrySimExamAssignmentToGuest> MinistrySimExamAssignmentsToGuests { get; set; }
    public DbSet<MinistrySimExamStudentAttempt> MinistrySimExamStudentAttempts { get; set; }
    public DbSet<MinistrySimExamStudentStageProgress> MinistrySimExamStudentStageProgresses { get; set; }
    public DbSet<MinistrySimExamStudentAnswer> MinistrySimExamStudentAnswers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers");

        //modelBuilder.Ignore<IdentityUser>();
        //modelBuilder.Ignore<IdentityRole>();
        //modelBuilder.Ignore<IdentityUserRole<string>>();
        //modelBuilder.Ignore<IdentityUserClaim<string>>();
        //modelBuilder.Ignore<IdentityUserLogin<string>>();
        //modelBuilder.Ignore<IdentityUserToken<string>>();
        //modelBuilder.Ignore<IdentityRoleClaim<string>>();

        // Branch - Project



        modelBuilder.Entity<Lecture>().ToTable("Lecture");

        modelBuilder.Entity<HomeworkSessionLog>()
            .HasOne(s => s.HomeworkSet)
            .WithMany()
            .HasForeignKey(s => s.HomeworkSetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeworkSessionLog>()
            .HasOne(s => s.Student)
            .WithMany()
            .HasForeignKey(s => s.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Project>()
            .HasOne(p => p.Branch)
            .WithMany(b => b.Projects)
            .HasForeignKey(p => p.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeworkArchiveAccess>()
            .HasIndex(x => new { x.HomeworkSetId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<HomeworkArchiveAccess>()
            .HasOne(x => x.HomeworkSet)
            .WithMany(x => x.ArchiveAccesses)
            .HasForeignKey(x => x.HomeworkSetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeworkArchiveAccess>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeworkArchiveAccess>()
            .HasOne(x => x.GrantedByUser)
            .WithMany()
            .HasForeignKey(x => x.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeBatchAccess>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeBatchAccess>()
            .HasOne(x => x.GrantedByUser)
            .WithMany()
            .HasForeignKey(x => x.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ===== RL: صفحة التسجيل العامة وطلبات الالتحاق =====
        modelBuilder.Entity<FrontendLeadCourse>(e =>
        {
            e.HasOne(x => x.FrontendLead)
             .WithMany(l => l.SelectedCourses)
             .HasForeignKey(x => x.FrontendLeadId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Course)
             .WithMany()
             .HasForeignKey(x => x.CourseId)
             .OnDelete(DeleteBehavior.SetNull);   // حذف دورة لا يحذف الطلب

            e.HasIndex(x => x.FrontendLeadId);
            e.HasIndex(x => x.CourseId);
        });

        modelBuilder.Entity<FrontendLead>()
            .HasIndex(x => new { x.Status, x.CreatedAt });

        modelBuilder.Entity<Project>()
            .HasIndex(p => new { p.ShowOnRegisterPage, p.IsActive });

        modelBuilder.Entity<Course>()
            .HasIndex(c => new { c.ProjectId, c.ShowOnRegisterPage, c.IsActive });

        // Project - Course
        modelBuilder.Entity<Course>()
            .HasOne(c => c.Project)
            .WithMany(p => p.Courses)
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Course - Challenge
        modelBuilder.Entity<Challenge>()
            .HasOne(c => c.Course)
            .WithMany()
            .HasForeignKey(c => c.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChallengeParticipation - Student
        modelBuilder.Entity<ChallengeParticipation>()
            .HasOne(cp => cp.Student)
            .WithMany(s => s.ChallengeParticipations)
            .HasForeignKey(cp => cp.StudentId)
            .OnDelete(DeleteBehavior.Restrict); // 🔄 تعديل هنا

        // ChallengeParticipation - Challenge
        modelBuilder.Entity<ChallengeParticipation>()
            .HasOne(cp => cp.Challenge)
            .WithMany(c => c.Participants)
            .HasForeignKey(cp => cp.ChallengeId)
            .OnDelete(DeleteBehavior.Restrict); // 🔄 تعديل هنا

        // Student - Batch
        // جدول الربط


        // Student - Parent
        modelBuilder.Entity<Student>()
            .HasOne(s => s.Parent)
            .WithMany(p => p.Students)
            .HasForeignKey(s => s.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Student - StudentAchievements
        modelBuilder.Entity<StudentAchievement>()
            .HasOne(sa => sa.Student)
            .WithMany(s => s.StudentAchievements)
            .HasForeignKey(sa => sa.StudentID)
            .OnDelete(DeleteBehavior.Restrict); // 🔄 تعديل هنا

        // StudentAchievement - Course
        modelBuilder.Entity<StudentAchievement>()
            .HasOne(sa => sa.Course)
            .WithMany()
            .HasForeignKey(sa => sa.CourseID)
            .OnDelete(DeleteBehavior.Restrict);

        // Student - StudentCourses
        modelBuilder.Entity<StudentCourse>()
            .HasOne(sc => sc.Student)
            .WithMany(s => s.StudentCourses)
            .HasForeignKey(sc => sc.StudentID)
            .OnDelete(DeleteBehavior.Restrict); // 🔄 تعديل

        modelBuilder.Entity<StudentCourse>()
      .HasOne(sc => sc.Course)
      .WithMany(c => c.StudentCourses)
      .HasForeignKey(sc => sc.CourseId)
      .OnDelete(DeleteBehavior.Restrict);


        // Student - StudentProgressRecords
        modelBuilder.Entity<Student>()
            .HasMany(s => s.StudentProgressRecords)
            .WithOne(sp => sp.Student)
            .HasForeignKey(sp => sp.StudentID)
            .OnDelete(DeleteBehavior.Restrict); // 🔄 تعديل

        // Student - StudyPlans
        modelBuilder.Entity<StudyPlan>()
            .HasOne(sp => sp.Student)
            .WithMany(s => s.StudyPlans)
            .HasForeignKey(sp => sp.StudentID)
            .OnDelete(DeleteBehavior.Restrict); // 🔄 تعديل

        // Student - RemedialPlans
        modelBuilder.Entity<RemedialPlan>()
            .HasOne(rp => rp.Student)
            .WithMany(s => s.RemedialPlans)
            .HasForeignKey(rp => rp.StudentID)
            .OnDelete(DeleteBehavior.Restrict); // 🔄 تعديل

        // RemedialPlan - Sessions
        modelBuilder.Entity<RemedialSession>()
            .HasOne(rs => rs.RemedialPlan)
            .WithMany(rp => rp.Sessions)
            .HasForeignKey(rs => rs.RemedialPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // RemedialSession - Student
        modelBuilder.Entity<RemedialSession>()
            .HasOne(rs => rs.Student)
            .WithMany()
            .HasForeignKey(rs => rs.StudentID)
            .OnDelete(DeleteBehavior.Restrict);

        // RemedialSession - Instructor
        modelBuilder.Entity<RemedialSession>()
            .HasOne(rs => rs.Instructor)
            .WithMany()
            .HasForeignKey(rs => rs.InstructorId)
            .OnDelete(DeleteBehavior.SetNull);

        // StudySessionReservation - Student
        modelBuilder.Entity<StudySessionReservation>()
            .HasOne(ssr => ssr.Student)
            .WithMany()
            .HasForeignKey(ssr => ssr.StudentID)
            .OnDelete(DeleteBehavior.Restrict);

        // StudySessionReservation - Instructor
        modelBuilder.Entity<StudySessionReservation>()
            .HasOne(ssr => ssr.Instructor)
            .WithMany()
            .HasForeignKey(ssr => ssr.InstructorId)
            .OnDelete(DeleteBehavior.SetNull);

        // StudySessionReservation - Branch
        modelBuilder.Entity<StudySessionReservation>()
            .HasOne(ssr => ssr.Branch)
            .WithMany()
            .HasForeignKey(ssr => ssr.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Branch - Course
        modelBuilder.Entity<Course>()
            .HasOne(c => c.Branch)
            .WithMany(b => b.Courses)
            .HasForeignKey(c => c.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // CourseInstructor (Many-to-Many)
        modelBuilder.Entity<CourseInstructor>()
            .HasKey(ci => new { ci.CourseID, ci.InstructorID });

        modelBuilder.Entity<CourseInstructor>()
            .HasOne(ci => ci.Course)
            .WithMany(c => c.CourseInstructors)
            .HasForeignKey(ci => ci.CourseID)
            .OnDelete(DeleteBehavior.Restrict); // 🔄

        modelBuilder.Entity<CourseInstructor>()
            .HasOne(ci => ci.Instructor)
            .WithMany(i => i.CourseInstructors)
            .HasForeignKey(ci => ci.InstructorID)
            .OnDelete(DeleteBehavior.Restrict); // 🔄

        modelBuilder.Entity<StudentPerformance>()
    .HasOne(sp => sp.Section)
    .WithMany(s => s.StudentPerformances)
    .HasForeignKey(sp => sp.SectionId)
    .OnDelete(DeleteBehavior.Restrict); // أو حسب ما تحب في حذف العلاقات

        modelBuilder.Entity<StudentPerformance>()
.HasOne(sp => sp.Curriculum)
.WithMany(c => c.StudentPerformances)
.HasForeignKey(sp => sp.CurriculumId)
.OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<Instructor>()
   .HasOne(i => i.User)
   .WithMany()
   .HasForeignKey(i => i.UserId)
   .OnDelete(DeleteBehavior.Restrict);

        // الربط مع المستخدم - Student
        modelBuilder.Entity<Student>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // الربط مع المستخدم - Parent
        modelBuilder.Entity<Parent>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);


        // ❌ منع الحذف التلقائي (Cascade Delete) للأسئلة أو الخيارات عند حذف الدروس أو المحاور
        // ✅ التأكد من ربط العلاقة بين Question و Lesson بشكل صريح

        modelBuilder.Entity<Question>()
            .HasOne(q => q.Lesson)
            .WithMany(l => l.Questions)
            .HasForeignKey(q => q.LessonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AttendanceRecord>()
    .HasOne(ar => ar.Lecture)
    .WithMany(l => l.AttendanceRecords)
    .HasForeignKey(ar => ar.LectureId)
    .OnDelete(DeleteBehavior.Restrict); // أو Cascade حسب الحاجة

        modelBuilder.Entity<Question>()
            .HasOne(q => q.Section)
            .WithMany()
            .HasForeignKey(q => q.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Question>()
            .HasOne(q => q.Curriculum)
            .WithMany()
            .HasForeignKey(q => q.CurriculumId)
            .OnDelete(DeleteBehavior.Restrict);

        // خيارات السؤال
        modelBuilder.Entity<QuestionOption>()
            .HasOne(qo => qo.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(qo => qo.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // نتائج الطالب
        modelBuilder.Entity<StudentExamResult>()
            .HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentExamResult>()
            .HasOne(e => e.Question)
            .WithMany()
            .HasForeignKey(e => e.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<QuestionAttemptNew>(entity =>
        {
            // ✅ المفتاح الأساسي
            entity.HasKey(a => a.Id);

            // ✅ فهرس فريد لحالات الاختبارات فقط (حين يكون ExamId موجود)
            entity.HasIndex(a => new { a.StudentId, a.QuestionId, a.ExamId })
                  .IsUnique()
                  .HasFilter("[ExamId] IS NOT NULL");

            // ✅ فهرس لاستعلامات واجبات الطالب (لوحة تحكم الواجبات الجديدة)
            // يخدم: WHERE StudentId = ? AND HomeworkSetId != null
            entity.HasIndex(a => new { a.StudentId, a.HomeworkSetId })
                  .HasDatabaseName("IX_QuestionAttemptNew_StudentId_HomeworkSetId");

            // ✅ فهرس لاستعلامات متوسط الدفعة على مستوى الواجب (تجميع كل الطلاب لكل واجب)
            // يخدم: WHERE HomeworkSetId != null GROUP BY HomeworkSetId, StudentId
            entity.HasIndex(a => new { a.HomeworkSetId, a.StudentId })
                  .HasDatabaseName("IX_QuestionAttemptNew_HomeworkSetId_StudentId");

            // ✅ العلاقة مع الطالب
            entity.HasOne(a => a.Student)
                  .WithMany()
                  .HasForeignKey(a => a.StudentId)
                  .OnDelete(DeleteBehavior.Restrict);

            // ✅ العلاقة مع السؤال
            entity.HasOne(a => a.Question)
                  .WithMany()
                  .HasForeignKey(a => a.QuestionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });




        // إعداد العلاقة Many-to-Many بين Instructor و Curriculum
        modelBuilder.Entity<CurriculumInstructor>()
            .HasKey(ci => ci.Id); // المفتاح الأساسي

        modelBuilder.Entity<CurriculumInstructor>()
            .HasOne(ci => ci.Instructor)
            .WithMany()
            .HasForeignKey(ci => ci.InstructorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CurriculumInstructor>()
        .HasOne(ci => ci.Curriculum)
        .WithMany(c => c.CurriculumInstructors) // ✅ تم ربط الـ Navigation Property بشكل صحيح
        .HasForeignKey(ci => ci.CurriculumId)
        .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RemedialPlanInteraction>()
  .HasOne(r => r.Student)
  .WithMany()
  .HasForeignKey(r => r.StudentId)
  .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RemedialSession>()
.HasOne(rs => rs.Section)
.WithMany()
.HasForeignKey(rs => rs.SectionId)
.OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Session>()
            .HasOne(s => s.Section)
            .WithMany(sec => sec.Sessions)
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lecture>()
    .HasOne(l => l.Batch)
    .WithMany()
    .HasForeignKey(l => l.BatchId)
    .OnDelete(DeleteBehavior.Restrict);




        modelBuilder.Entity<Student>()
            .HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");



        modelBuilder.Entity<StudentCourseEnrollment>()
.HasOne(e => e.Student)
.WithMany(s => s.StudentCourseEnrollments)
.HasForeignKey(e => e.StudentId)
.OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentCourseEnrollment>()
            .HasOne(e => e.Course)
            .WithMany(c => c.StudentCourseEnrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InstructorCurriculumBatch>()
    .HasOne(icb => icb.Instructor)
    .WithMany()
    .HasForeignKey(icb => icb.InstructorId)
    .OnDelete(DeleteBehavior.Restrict); // ✅ مهم

        modelBuilder.Entity<InstructorCurriculumBatch>()
            .HasOne(icb => icb.Curriculum)
            .WithMany()
            .HasForeignKey(icb => icb.CurriculumId)
            .OnDelete(DeleteBehavior.Restrict); // ✅ مهم

        modelBuilder.Entity<InstructorCurriculumBatch>()
            .HasOne(icb => icb.Batch)
            .WithMany()
            .HasForeignKey(icb => icb.BatchId)
            .OnDelete(DeleteBehavior.Restrict); // ✅ مهم

        modelBuilder.Entity<InstructorCurriculumBatch>()
            .HasIndex(icb => new { icb.InstructorId, icb.CurriculumId, icb.BatchId })
            .IsUnique();

        modelBuilder.Entity<SectionUnit>()
.HasKey(su => new { su.SectionId, su.UnitId });

        modelBuilder.Entity<SectionUnit>()
            .HasOne(su => su.Section)
            .WithMany(s => s.SectionUnits)
            .HasForeignKey(su => su.SectionId)
            .OnDelete(DeleteBehavior.Restrict); // لتجنب مشكلة multiple cascade

        modelBuilder.Entity<SectionUnit>()
            .HasOne(su => su.Unit)
            .WithMany(u => u.SectionUnits)
            .HasForeignKey(su => su.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assignment>()
.HasOne(a => a.Curriculum)
.WithMany()
.HasForeignKey(a => a.CurriculumId)
.OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assignment>()
            .HasOne(a => a.Section)
            .WithMany()
            .HasForeignKey(a => a.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assignment>()
            .HasOne(a => a.Lesson)
            .WithMany()
            .HasForeignKey(a => a.LessonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assignment>()
            .HasOne(a => a.Batch)
            .WithMany()
            .HasForeignKey(a => a.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // نفس الفكرة لبقية الكيانات لو احتجت مثل MockExam, Homework, EnhancementSkillSession...

        modelBuilder.Entity<AssignmentQuestion>()
            .HasOne(aq => aq.Assignment)
            .WithMany(a => a.Questions)
            .HasForeignKey(aq => aq.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade); // ده ممكن تسيبه كاسكيد

        modelBuilder.Entity<ExamQuestion>()
          .HasKey(eq => eq.Id);


        modelBuilder.Entity<ExamQuestion>()
            .HasOne(eq => eq.Question)
            .WithMany() // أو WithMany(q => q.ExamQuestions) لو فيه علاقة عكسية
            .HasForeignKey(eq => eq.QuestionId);

        modelBuilder.Entity<ExamStudentQuestionOrder>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.StudentId, x.ExamAssignmentId, x.QuestionId });
            entity.HasIndex(x => new { x.StudentId, x.ExamAssignmentId, x.OrderNumber });
            entity.HasIndex(x => new { x.StudentId, x.ExamAssignmentToStudentId, x.QuestionId });
            entity.HasIndex(x => new { x.StudentId, x.ExamAssignmentToStudentId, x.OrderNumber });

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .HasPrincipalKey(x => x.StudentID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExamAssignment)
                .WithMany()
                .HasForeignKey(x => x.ExamAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExamAssignmentToStudent)
                .WithMany()
                .HasForeignKey(x => x.ExamAssignmentToStudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Exam)
                .WithMany()
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Lesson>()
  .HasOne(l => l.Section)
  .WithMany()
  .HasForeignKey(l => l.SectionId)
  .OnDelete(DeleteBehavior.Restrict); // 👈 إيقاف الحذف المتسلسل

        modelBuilder.Entity<Lesson>()
      .HasOne(l => l.Lecture)
      .WithMany(l => l.Lessons)
      .HasForeignKey(l => l.LectureId)
      .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudySessionReservation>()
    .Property(s => s.FinalFee)
    .HasPrecision(18, 2);



        // ✅ تعطيل الحذف التلقائي بين Homework و HomeworkSet
        modelBuilder.Entity<Homework>()
            .HasOne(h => h.HomeworkSet)
            .WithMany(hs => hs.Homeworks)
            .HasForeignKey(h => h.HomeworkSetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Homework>()
            .HasOne(h => h.Student)
            .WithMany()
            .HasForeignKey(h => h.StudentId)
            .HasPrincipalKey(s => s.StudentID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeworkSet>()
            .HasOne(hs => hs.Lecture)
            .WithMany()
            .HasForeignKey(hs => hs.LectureId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<ExamAssignmentToBatch>()
    .HasOne(e => e.Exam)
    .WithMany()
    .HasForeignKey(e => e.ExamId)
    .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignmentToBatch>()
            .HasOne(e => e.Batch)
            .WithMany()
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignmentToBatch>()
            .HasOne(e => e.Section)
            .WithMany()
            .HasForeignKey(e => e.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignmentToBatch>()
            .HasOne(e => e.Curriculum)
            .WithMany()
            .HasForeignKey(e => e.CurriculumId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignmentToBatch>()
            .HasOne(e => e.Lesson)
            .WithMany()
            .HasForeignKey(e => e.LessonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignmentToBatch>()
        .HasMany(e => e.Questions)
        .WithOne(eq => eq.ExamAssignment)
        .HasForeignKey(eq => eq.ExamAssignmentId)
        .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<StudentPost>()
        .HasOne(p => p.Student)
        .WithMany()
        .HasForeignKey(p => p.StudentID)
        .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentPost>()
            .HasOne(p => p.Batch)
            .WithMany()
            .HasForeignKey(p => p.BatchId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<CourseCurriculum>(entity =>
        {
            entity.ToTable("CourseCurriculums");

            entity.HasKey(cc => cc.Id);

            entity.HasOne(cc => cc.Course)
                  .WithMany(c => c.CourseCurriculums)
                  .HasForeignKey(cc => cc.CourseId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(cc => cc.Curriculum)
                  .WithMany(cu => cu.CourseCurriculums)
                  .HasForeignKey(cc => cc.CurriculumId)
                  .OnDelete(DeleteBehavior.Restrict);
        });


        modelBuilder.Entity<Question>()
    .HasOne(q => q.VerbalPassage)
    .WithMany(vp => vp.Questions)
    .HasForeignKey(q => q.VerbalPassageId)
    .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<ExamAssignmentToBatch>()
    .HasOne(e => e.CreatedByInstructor)
    .WithMany()
    .HasForeignKey(e => e.CreatedByInstructorId)
    .OnDelete(DeleteBehavior.Restrict); // لتفادي الحذف التلقائي

        modelBuilder.Entity<BatchLessonCompletion>()
          .HasOne(b => b.Section)
          .WithMany()
          .HasForeignKey(b => b.SectionId)
          .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BatchLessonCompletion>()
            .HasOne(b => b.Lecture)
            .WithMany()
            .HasForeignKey(b => b.LectureId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignment>()
    .HasOne(ea => ea.Exam)
    .WithMany()
    .HasForeignKey(ea => ea.ExamId)
    .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignment>()
            .HasOne(ea => ea.Student)
            .WithMany()
            .HasForeignKey(ea => ea.StudentId)
            .OnDelete(DeleteBehavior.Restrict);



        modelBuilder.Entity<HomeworkSetAttempt>()
    .HasOne(h => h.HomeworkSet)
    .WithMany(hs => hs.Attempts) // لازم تضيف ICollection<HomeworkSetAttempt> Attempts في HomeworkSet
    .HasForeignKey(h => h.HomeworkSetId)
    .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeworkSetAttempt>()
            .HasOne(h => h.Student)
            .WithMany()
            .HasForeignKey(h => h.StudentId)
            .HasPrincipalKey(s => s.StudentID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeworkSetSection>()
            .HasOne(hss => hss.HomeworkSet)
            .WithMany(hs => hs.Sections) // لازم تضيف ICollection<HomeworkSetSection> Sections في HomeworkSet
            .HasForeignKey(hss => hss.HomeworkSetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeworkSetSection>()
            .HasOne(hss => hss.Section)
            .WithMany()
            .HasForeignKey(hss => hss.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HomeworkSetStudent>()
            .HasOne(hss => hss.HomeworkSet)
            .WithMany(hs => hs.Students) // لازم تضيف ICollection<HomeworkSetStudent> Students في HomeworkSet
            .HasForeignKey(hss => hss.HomeworkSetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeworkSetStudent>()
            .HasOne(hss => hss.Student)
            .WithMany()
            .HasForeignKey(hss => hss.StudentId)
            .HasPrincipalKey(s => s.StudentID)
            .OnDelete(DeleteBehavior.Restrict);



        // إلغاء الكاسكيد بين StudentRankHistories و Students
        modelBuilder.Entity<StudentRankHistory>()
            .HasOne(sr => sr.Student)
            .WithMany()
            .HasForeignKey(sr => sr.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // إلغاء الكاسكيد بين StudentRankHistories و Batches
        modelBuilder.Entity<StudentRankHistory>()
            .HasOne(sr => sr.Batch)
            .WithMany()
            .HasForeignKey(sr => sr.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Question>()
    .HasOne(q => q.Partner)
    .WithMany()
    .HasForeignKey(q => q.PartnerId)
    .OnDelete(DeleteBehavior.Restrict);


        // ==== StudentBatchEnrollment (العلاقة الموحدة والصحيحة) ====

        modelBuilder.Entity<StudentBatchEnrollment>(entity =>
        {
            entity.ToTable("StudentBatchEnrollments");

            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StudentID, e.BatchId }).IsUnique();

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.HasOne(e => e.Student)
                  .WithMany(s => s.BatchEnrollments) // ← استخدام اسم الملاحة في Student
                  .HasForeignKey(e => e.StudentID)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Batch)
                  .WithMany(b => b.StudentBatchEnrollments) // ← استخدام اسم الملاحة في Batch
                  .HasForeignKey(e => e.BatchId)
                  .OnDelete(DeleteBehavior.Restrict);


            // EnhancementSetIndicator cascade
            modelBuilder.Entity<EnhancementSetIndicator>()
                .HasOne(i => i.EnhancementSkillSet)
                .WithMany(s => s.Indicators)
                .HasForeignKey(i => i.EnhancementSkillSetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EnhancementSetIndicator>()
                .HasOne(i => i.Lesson)
                .WithMany()
                .HasForeignKey(i => i.LessonId)
                .OnDelete(DeleteBehavior.Restrict);

            // EnhancementSkillSet → ProfessionalModel
            modelBuilder.Entity<EnhancementSkillSet>()
                .HasOne(s => s.ProfessionalModel)
                .WithMany()
                .HasForeignKey(s => s.ProfessionalModelId)
                .OnDelete(DeleteBehavior.SetNull);

            // منع تعدد الـ Cascade Delete في EnhancementSkills
            modelBuilder.Entity<EnhancementSkillAssignment>()
     .HasOne(a => a.EnhancementSkillSet)
     .WithMany(s => s.Assignments)
     .HasForeignKey(a => a.EnhancementSkillSetId)
     .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EnhancementSkillAssignment>()
                .HasOne(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EnhancementSkillAssignment>()
                .HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EnhancementSkillResult>()
                .HasOne(r => r.EnhancementSkillSet)
                .WithMany()
                .HasForeignKey(r => r.EnhancementSkillSetId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EnhancementSkillResult>()
                .HasOne(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // لضمان توافق SQL Server 2014 مع Decimal Precision
            modelBuilder.Entity<RemedialSession>()
                .Property(p => p.Fee)
                .HasPrecision(10, 2);

            // فهارس بسيطة لتحسين الأداء
            modelBuilder.Entity<StudentIndicatorResult>()
                .HasIndex(x => new { x.StudentId, x.SectionId });

            modelBuilder.Entity<IndicatorRemedialCorrelation>()
                .HasIndex(x => new { x.StudentId, x.SectionId });

            // 🔸 منع أخطاء Multiple Cascade Paths في العلاقات مع Section
            modelBuilder.Entity<PerformanceIndicatorExamSection>()
                .HasOne(p => p.Section)
                .WithMany()
                .HasForeignKey(p => p.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentIndicatorResult>()
                .HasOne(r => r.Section)
                .WithMany()
                .HasForeignKey(r => r.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IndicatorRemedialCorrelation>()
                .HasOne(c => c.Section)
                .WithMany()
                .HasForeignKey(c => c.SectionId)
                .OnDelete(DeleteBehavior.Restrict);



            // 🔹 منع Multiple Cascade Paths على علاقة StudentIndicatorResult -> Student
            modelBuilder.Entity<StudentIndicatorResult>()
                .HasOne(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔹 ونضيف نفس الشيء لباقي الكيانات الجديدة التي ترتبط بالطالب
            modelBuilder.Entity<IndicatorRemedialCorrelation>()
                .HasOne(c => c.Student)
                .WithMany()
                .HasForeignKey(c => c.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PerformanceIndicatorExam>()
                .HasOne(p => p.Batch)
                .WithMany()
                .HasForeignKey(p => p.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PerformanceIndicatorExam>()
                .HasOne(p => p.Curriculum)
                .WithMany()
                .HasForeignKey(p => p.CurriculumId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QuestionAttemptNew>()
                  .HasOne(q => q.PerformanceIndicatorExam)
                  .WithMany()
                  .HasForeignKey(q => q.PerformanceIndicatorExamId)
                  .OnDelete(DeleteBehavior.NoAction);


            modelBuilder.Entity<ProfessionalModelPartner>()
                .HasIndex(x => new { x.ProfessionalModelId, x.PartnerId })
                .IsUnique();

            modelBuilder.Entity<ProfessionalModelSubscriptionPeriod>()
                .HasIndex(x => new { x.ProfessionalModelId, x.PartnerSubscriptionPeriodId })
                .IsUnique();
            modelBuilder.Entity<AdminPermission>()
                .HasIndex(p => new { p.ProfileId, p.ModuleId })
                .IsUnique();

            modelBuilder.Entity<AdminPermissionModule>()
                .HasIndex(m => m.Key)
                .IsUnique();

            modelBuilder.Entity<AdminProfileControllerPermission>()
                .HasIndex(x => new { x.AdminProfileId, x.ControllerName })
                .IsUnique();

            modelBuilder.Entity<AdminProfileCustomPermission>()
                .HasIndex(x => new { x.AdminProfileControllerPermissionId, x.ActionName })
                .IsUnique();

            modelBuilder.Entity<Instructor>()
             .HasQueryFilter(x => !x.IsDeleted);



            modelBuilder.Entity<InstructorCriticalQuestionTask>()
    .HasMany(x => x.Items)
    .WithOne(x => x.Task)
    .HasForeignKey(x => x.InstructorCriticalQuestionTaskId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InstructorCriticalQuestionTaskItem>()
                .HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InstructorCriticalQuestionTask>()
                .HasOne(x => x.Instructor)
                .WithMany()
                .HasForeignKey(x => x.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InstructorCriticalQuestionTask>()
                .HasOne(x => x.Batch)
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InstructorCriticalQuestionTask>()
                .HasOne(x => x.Lesson)
                .WithMany()
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Restrict);



        });

        modelBuilder.Entity<Lecture>()
            .HasOne(l => l.StartedByUser)
            .WithMany()
            .HasForeignKey(l => l.StartedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lecture>()
            .HasOne(l => l.EndedByUser)
            .WithMany()
            .HasForeignKey(l => l.EndedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlacementExamBatchArchiveAccess>()
            .HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlacementExamBatchArchiveAccess>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlacementExamBatchArchiveAccess>()
            .HasOne(x => x.GrantedByUser)
            .WithMany()
            .HasForeignKey(x => x.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignmentBatchArchiveAccess>()
            .HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignmentBatchArchiveAccess>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExamAssignmentBatchArchiveAccess>()
            .HasOne(x => x.GrantedByUser)
            .WithMany()
            .HasForeignKey(x => x.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PerformanceIndicatorExamArchiveAccess>()
            .HasOne(x => x.Exam)
            .WithMany()
            .HasForeignKey(x => x.ExamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PerformanceIndicatorExamArchiveAccess>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PerformanceIndicatorExamArchiveAccess>()
            .HasOne(x => x.GrantedByUser)
            .WithMany()
            .HasForeignKey(x => x.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Professional Certificates Module
        modelBuilder.Entity<ProfessionalCertificateCourse>()
            .HasIndex(x => x.Slug)
            .IsUnique();

        modelBuilder.Entity<ProfessionalCertificateRegistration>()
            .HasIndex(x => x.ProfessionalCertificateCourseId);

        modelBuilder.Entity<ProfessionalCertificateRegistration>()
            .HasIndex(x => x.NationalId);

        modelBuilder.Entity<ProfessionalCertificateRegistration>()
            .HasIndex(x => x.PhoneNumber);

        modelBuilder.Entity<ProfessionalCertificateRegistration>()
            .HasIndex(x => x.Status);

        modelBuilder.Entity<ProfessionalCertificateRegistration>()
            .HasIndex(x => x.SubmittedAt);

        modelBuilder.Entity<ProfessionalCertificateRegistration>()
            .HasOne(x => x.ProfessionalCertificateCourse)
            .WithMany(x => x.Registrations)
            .HasForeignKey(x => x.ProfessionalCertificateCourseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProfessionalCertificateSectionSetting>()
            .HasData(new ProfessionalCertificateSectionSetting
            {
                Id = 1,
                IsEnabled = true,
                Title = "الشهادات الدولية الاحترافية",
                Subtitle = "برامج متخصصة في السلامة والصحة المهنية بمعايير عالمية",
                Description = "اختر البرنامج المناسب لك وسجّل بياناتك، وسيقوم فريق الدعم الفني بالتواصل معك لاستكمال تفاصيل الاشتراك.",
                ButtonText = "سجل اهتمامك",
                AutoCloseWhenMaxReached = false
            });

        // Course Collections Module (Epic A)
        modelBuilder.Entity<CourseCollection>()
            .HasIndex(x => x.Slug)
            .IsUnique();

        modelBuilder.Entity<CourseCollectionCourse>()
            .HasIndex(x => new { x.CourseCollectionId, x.Slug })
            .IsUnique();

        modelBuilder.Entity<CourseCollectionCourse>()
            .HasIndex(x => x.CourseCollectionId);

        modelBuilder.Entity<CourseCollectionSponsor>()
            .HasIndex(x => x.CourseCollectionId);

        modelBuilder.Entity<CourseCollectionRegistration>()
            .HasIndex(x => x.CourseCollectionCourseId);

        modelBuilder.Entity<CourseCollectionRegistration>()
            .HasIndex(x => x.NationalId);

        modelBuilder.Entity<CourseCollectionRegistration>()
            .HasIndex(x => x.PhoneNumber);

        modelBuilder.Entity<CourseCollectionRegistration>()
            .HasIndex(x => x.Status);

        modelBuilder.Entity<CourseCollectionRegistration>()
            .HasIndex(x => x.SubmittedAt);

        modelBuilder.Entity<CourseCollectionCourse>()
            .HasOne(x => x.CourseCollection)
            .WithMany(x => x.Courses)
            .HasForeignKey(x => x.CourseCollectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CourseCollectionSponsor>()
            .HasOne(x => x.CourseCollection)
            .WithMany(x => x.Sponsors)
            .HasForeignKey(x => x.CourseCollectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CourseCollectionRegistration>()
            .HasOne(x => x.CourseCollectionCourse)
            .WithMany(x => x.Registrations)
            .HasForeignKey(x => x.CourseCollectionCourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // ==== 🚀 فهارس تحسين الأداء (تقرير Qdrat_Performance_Report.md) ====

        // Index #1 — QuestionOptions(QuestionId): كل سؤال يُحمَّل مع خياراته باستمرار
        Microsoft.EntityFrameworkCore.SqlServerIndexBuilderExtensions.IncludeProperties(
            modelBuilder.Entity<QuestionOption>()
                .HasIndex(o => o.QuestionId)
                .HasDatabaseName("IX_QuestionOptions_QuestionId"),
            o => new { o.ImageUrl, o.Text });

        // Index #2 — Notifications(StudentId, IsRead): قراءة إشعارات الطالب غير المقروءة
        Microsoft.EntityFrameworkCore.SqlServerIndexBuilderExtensions.IncludeProperties(
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.StudentID, n.IsRead })
                .HasDatabaseName("IX_Notifications_StudentId_IsRead"),
            n => new { n.Category, n.SentAt, n.TargetUrl });

        // Index #3 — Homeworks(StudentId, HomeworkSetId): الاستعلامات الفعلية تبدأ دائماً بهذا الترتيب
        Microsoft.EntityFrameworkCore.SqlServerIndexBuilderExtensions.IncludeProperties(
            modelBuilder.Entity<Homework>()
                .HasIndex(h => new { h.StudentId, h.HomeworkSetId })
                .HasDatabaseName("IX_Homeworks_StudentId_HomeworkSetId"),
            h => new { h.QuestionId });

        // Index #4 — QuestionAttemptNew(StudentId, HomeworkSetId): نفس الترتيب المستخدم فعلياً في الاستعلامات
        Microsoft.EntityFrameworkCore.SqlServerIndexBuilderExtensions.IncludeProperties(
            modelBuilder.Entity<QuestionAttemptNew>()
                .HasIndex(a => new { a.StudentId, a.HomeworkSetId })
                .HasDatabaseName("IX_QuestionAttemptNew_StudentId_HomeworkSetId"),
            a => new { a.QuestionId, a.IsCorrect, a.AttemptedAt, a.SelectedAnswer, a.TimeTakenSeconds });

        // ملاحظة: Students(UserId) لديه بالفعل فهرس فريد (سطر ~586 أعلاه) فلا حاجة لفهرس إضافي.

        // ===== MinistrySimExam (اختبار محاكاة اختبار الوزارة) — Sprint 1 (MSE-A) =====

        modelBuilder.Entity<MinistrySimExam>()
            .HasOne(e => e.Course)
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExam>()
            .HasOne(e => e.CreatedByInstructor)
            .WithMany()
            .HasForeignKey(e => e.CreatedByInstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamStage>()
            .HasOne(s => s.MinistrySimExam)
            .WithMany(e => e.Stages)
            .HasForeignKey(s => s.MinistrySimExamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MinistrySimExamStage>()
            .HasOne(s => s.QuantSection)
            .WithMany()
            .HasForeignKey(s => s.QuantSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamStage>()
            .HasOne(s => s.VerbalSection)
            .WithMany()
            .HasForeignKey(s => s.VerbalSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamStage>()
            .HasIndex(s => new { s.MinistrySimExamId, s.StageNumber })
            .IsUnique();

        modelBuilder.Entity<MinistrySimExamStageIndicatorSelection>()
            .HasOne(x => x.MinistrySimExamStage)
            .WithMany(s => s.IndicatorSelections)
            .HasForeignKey(x => x.MinistrySimExamStageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MinistrySimExamStageIndicatorSelection>()
            .HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamStageIndicatorSelection>()
            .HasOne(x => x.Lesson)
            .WithMany()
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamStageQuestion>()
            .HasOne(x => x.MinistrySimExamStage)
            .WithMany(s => s.Questions)
            .HasForeignKey(x => x.MinistrySimExamStageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MinistrySimExamStageQuestion>()
            .HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamAssignmentToBatch>()
            .HasOne(x => x.MinistrySimExam)
            .WithMany(e => e.AssignmentsToBatches)
            .HasForeignKey(x => x.MinistrySimExamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamAssignmentToBatch>()
            .HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamAssignmentToBatch>()
            .HasOne(x => x.CreatedByInstructor)
            .WithMany()
            .HasForeignKey(x => x.CreatedByInstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamAssignmentToStudent>()
            .HasOne(x => x.MinistrySimExam)
            .WithMany(e => e.AssignmentsToStudents)
            .HasForeignKey(x => x.MinistrySimExamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamAssignmentToStudent>()
            .HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .HasPrincipalKey(s => s.StudentID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamAssignmentToGuest>()
            .HasOne(x => x.MinistrySimExam)
            .WithMany(e => e.AssignmentsToGuests)
            .HasForeignKey(x => x.MinistrySimExamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamAssignmentToGuest>()
            .HasOne(x => x.GuestStudent)
            .WithMany(g => g.Assignments)
            .HasForeignKey(x => x.GuestStudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamAssignmentToGuest>()
            .HasIndex(x => new { x.MinistrySimExamId, x.GuestStudentId })
            .IsUnique();

        modelBuilder.Entity<MinistrySimExamStudentAttempt>()
            .HasOne(x => x.MinistrySimExam)
            .WithMany(e => e.StudentAttempts)
            .HasForeignKey(x => x.MinistrySimExamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamStudentAttempt>()
            .HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .HasPrincipalKey(s => s.StudentID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MinistrySimExamStudentAttempt>()
            .HasIndex(x => new { x.MinistrySimExamId, x.StudentId })
            .IsUnique();

        modelBuilder.Entity<MinistrySimExamStudentStageProgress>()
            .HasOne(x => x.Attempt)
            .WithMany(a => a.StageProgress)
            .HasForeignKey(x => x.MinistrySimExamStudentAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MinistrySimExamStudentStageProgress>()
            .HasIndex(x => new { x.MinistrySimExamStudentAttemptId, x.StageNumber })
            .IsUnique();

        modelBuilder.Entity<MinistrySimExamStudentAnswer>()
            .HasOne(x => x.Attempt)
            .WithMany(a => a.Answers)
            .HasForeignKey(x => x.MinistrySimExamStudentAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MinistrySimExamStudentAnswer>()
            .HasOne(x => x.StageQuestion)
            .WithMany()
            .HasForeignKey(x => x.MinistrySimExamStageQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ==== IntegrityViolationLog (Translation Guard — QG-H2: فهرس فريد مُصفّى يمنع تكرار صف نشط عند طلبين متزامنين) ====
        modelBuilder.Entity<IntegrityViolationLog>()
            .HasIndex(x => new { x.StudentId, x.AttemptType, x.AttemptEntityId })
            .IsUnique()
            .HasFilter("[IsResolved] = 0")
            .HasDatabaseName("IX_IntegrityViolationLogs_ActiveViolation");

    }






}
