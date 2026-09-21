
        namespace QdratNew.Services.Students.Abstractions
        {
            public interface IStudentCourseContext
            {
                int? ActiveCourseId { get; }
                int? ActiveBatchId { get; }

                void SetSelection(int courseId, int batchId);
                void ClearSelection();
                bool HasSelection();
            }
        }




