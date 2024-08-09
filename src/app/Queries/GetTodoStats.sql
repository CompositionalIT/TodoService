SELECT CompletionState, COUNT(*) AS TodoItems
FROM
(
    SELECT
        CASE WHEN (CompletedDate IS NULL) THEN 'Incomplete' ELSE 'Complete' END AS CompletionState
    FROM dbo.Todo
) src
GROUP BY CompletionState
