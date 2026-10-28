START TRANSACTION;
CREATE TABLE `WorldRestores` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OwnerId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SourceProjectId` char(36) COLLATE ascii_general_ci NOT NULL,
    `TargetProjectId` char(36) COLLATE ascii_general_ci NOT NULL,
    `State` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `Plan` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Uploaded` longtext CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_WorldRestores` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_WorldRestores_OwnerId_SourceProjectId` ON `WorldRestores` (`OwnerId`, `SourceProjectId`);

CREATE INDEX `IX_WorldRestores_State_UpdatedAt` ON `WorldRestores` (`State`, `UpdatedAt`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261026033651_AddWorldRestore', '9.0.0');

COMMIT;

