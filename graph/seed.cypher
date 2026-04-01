// ============================================================
// DKNet.Templates — Code Graph Seed
// Source: src/ApiEndpoints/**
// Run via: graph/load.sh   OR   cypher-shell < graph/seed.cypher
// ============================================================

// Clear existing graph
MATCH (n) DETACH DELETE n;

// ==================== ARCHITECTURAL LAYERS ====================
MERGE (:Layer {name: 'Api',         order: 1, description: 'Entry point, HTTP endpoints, auth, OpenAPI'});
MERGE (:Layer {name: 'AppServices', order: 2, description: 'CQRS handlers, validators, DTOs, events'});
MERGE (:Layer {name: 'Domains',     order: 3, description: 'Entities, aggregate roots, value objects'});
MERGE (:Layer {name: 'Infra',       order: 4, description: 'EF Core, repos, event publisher, service bus'});
MERGE (:Layer {name: 'Share',       order: 5, description: 'Shared constants, options, base types'});
MERGE (:Layer {name: 'AppHost',     order: 6, description: 'Aspire orchestration only, no business logic'});
MERGE (:Layer {name: 'Tests',       order: 7, description: 'Unit and integration tests'});

// ==================== PROJECTS ====================
MERGE (:Project {name: 'Minimal.Share',         framework: 'net10.0', type: 'Library'});
MERGE (:Project {name: 'Minimal.Domains',       framework: 'net10.0', type: 'Library'});
MERGE (:Project {name: 'Minimal.AppServices',   framework: 'net10.0', type: 'Library'});
MERGE (:Project {name: 'Minimal.Infra',         framework: 'net10.0', type: 'Library'});
MERGE (:Project {name: 'Minimal.Api',           framework: 'net10.0', type: 'Web', containerImage: 'mcr.microsoft.com/dotnet/aspnet:10.0-alpine'});
MERGE (:Project {name: 'Minimal.AppHost',       framework: 'net10.0', type: 'AppHost'});
MERGE (:Project {name: 'Minimal.App.Tests',     framework: 'net10.0', type: 'Test'});
MERGE (:Project {name: 'Minimal.App.BDDTests',  framework: 'net10.0', type: 'Test'});

// Project → Layer
MATCH (p:Project {name: 'Minimal.Share'}),        (l:Layer {name: 'Share'})       MERGE (p)-[:IN_LAYER]->(l);
MATCH (p:Project {name: 'Minimal.Domains'}),      (l:Layer {name: 'Domains'})     MERGE (p)-[:IN_LAYER]->(l);
MATCH (p:Project {name: 'Minimal.AppServices'}),  (l:Layer {name: 'AppServices'}) MERGE (p)-[:IN_LAYER]->(l);
MATCH (p:Project {name: 'Minimal.Infra'}),        (l:Layer {name: 'Infra'})       MERGE (p)-[:IN_LAYER]->(l);
MATCH (p:Project {name: 'Minimal.Api'}),          (l:Layer {name: 'Api'})         MERGE (p)-[:IN_LAYER]->(l);
MATCH (p:Project {name: 'Minimal.AppHost'}),      (l:Layer {name: 'AppHost'})     MERGE (p)-[:IN_LAYER]->(l);
MATCH (p:Project {name: 'Minimal.App.Tests'}),    (l:Layer {name: 'Tests'})       MERGE (p)-[:IN_LAYER]->(l);
MATCH (p:Project {name: 'Minimal.App.BDDTests'}), (l:Layer {name: 'Tests'})       MERGE (p)-[:IN_LAYER]->(l);

// Project → Project dependencies (ProjectReferences from .csproj)
MATCH (a:Project {name: 'Minimal.Domains'}),     (b:Project {name: 'Minimal.Share'})       MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.AppServices'}), (b:Project {name: 'Minimal.Domains'})     MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.Infra'}),       (b:Project {name: 'Minimal.AppServices'}) MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.Infra'}),       (b:Project {name: 'Minimal.Share'})       MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.Infra'}),       (b:Project {name: 'Minimal.Domains'})     MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.Api'}),         (b:Project {name: 'Minimal.AppServices'}) MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.Api'}),         (b:Project {name: 'Minimal.Infra'})       MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.AppHost'}),     (b:Project {name: 'Minimal.Api'})         MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.App.Tests'}),   (b:Project {name: 'Minimal.Api'})         MERGE (a)-[:DEPENDS_ON]->(b);
MATCH (a:Project {name: 'Minimal.App.BDDTests'}),(b:Project {name: 'Minimal.Api'})         MERGE (a)-[:DEPENDS_ON]->(b);

// ==================== NUGET PACKAGES ====================
MERGE (:NugetPackage {name: 'DKNet.EfCore.Abstractions',      group: 'DKNet',      version: '10.0.25'});
MERGE (:NugetPackage {name: 'DKNet.EfCore.DataAuthorization', group: 'DKNet',      version: '10.0.25'});
MERGE (:NugetPackage {name: 'DKNet.EfCore.Specifications',    group: 'DKNet',      version: '10.0.25'});
MERGE (:NugetPackage {name: 'DKNet.EfCore.DtoGenerator',      group: 'DKNet',      version: '10.0.25'});
MERGE (:NugetPackage {name: 'DKNet.EfCore.Events',            group: 'DKNet',      version: '10.0.25'});
MERGE (:NugetPackage {name: 'DKNet.EfCore.Relational.Helpers',group: 'DKNet',      version: '10.0.25'});
MERGE (:NugetPackage {name: 'DKNet.EfCore.AuditLogs',         group: 'DKNet',      version: '10.0.21'});
MERGE (:NugetPackage {name: 'DKNet.SlimBus.Extensions',       group: 'DKNet',      version: '10.0.25'});
MERGE (:NugetPackage {name: 'FluentValidation',               group: 'Community',  version: '12.1.1'});
MERGE (:NugetPackage {name: 'FluentResults',                  group: 'Community',  version: ''});
MERGE (:NugetPackage {name: 'Mapster',                        group: 'Community',  version: '7.4.0'});
MERGE (:NugetPackage {name: 'SlimMessageBus',                 group: 'Community',  version: '3.0.0'});
MERGE (:NugetPackage {name: 'SlimMessageBus.Host.Memory',     group: 'Community',  version: '3.4.0'});
MERGE (:NugetPackage {name: 'SlimMessageBus.Host.AzureServiceBus', group: 'Community', version: '3.4.0'});
MERGE (:NugetPackage {name: 'Scrutor',                        group: 'Community',  version: ''});
MERGE (:NugetPackage {name: 'Microsoft.EntityFrameworkCore.SqlServer', group: 'Microsoft', version: '10.0.5'});
MERGE (:NugetPackage {name: 'Asp.Versioning.Http',            group: 'Microsoft',  version: '8.1.1'});
MERGE (:NugetPackage {name: 'OpenTelemetry',                  group: 'Microsoft',  version: '1.15.1'});
MERGE (:NugetPackage {name: 'Microsoft.Extensions.Caching.Hybrid', group: 'Microsoft', version: '10.4.0'});
MERGE (:NugetPackage {name: 'Microsoft.FeatureManagement.AspNetCore', group: 'Microsoft', version: ''});
MERGE (:NugetPackage {name: 'Aspire.Hosting.AppHost',         group: 'Microsoft',  version: '13.2.0'});
MERGE (:NugetPackage {name: 'Aspire.Hosting.Redis',           group: 'Microsoft',  version: '13.2.0'});
MERGE (:NugetPackage {name: 'Aspire.Hosting.SqlServer',       group: 'Microsoft',  version: '13.2.0'});
MERGE (:NugetPackage {name: 'xunit',                          group: 'Community',  version: '2.9.3'});
MERGE (:NugetPackage {name: 'NetArchTest.Rules',              group: 'Community',  version: '1.3.2'});
MERGE (:NugetPackage {name: 'Reqnroll.NUnit',                 group: 'Community',  version: '3.3.4'});
MERGE (:NugetPackage {name: 'Meziantou.Analyzer',             group: 'Community',  version: '2.0.239'});
MERGE (:NugetPackage {name: 'SonarAnalyzer.CSharp',           group: 'Community',  version: '10.15.0'});

// Project → NuGet packages
MATCH (p:Project {name: 'Minimal.Share'}),       (pkg:NugetPackage {name: 'FluentResults'})                         MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Domains'}),     (pkg:NugetPackage {name: 'DKNet.EfCore.Abstractions'})             MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Domains'}),     (pkg:NugetPackage {name: 'DKNet.EfCore.DataAuthorization'})        MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppServices'}), (pkg:NugetPackage {name: 'DKNet.EfCore.DtoGenerator'})             MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppServices'}), (pkg:NugetPackage {name: 'DKNet.EfCore.Specifications'})           MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppServices'}), (pkg:NugetPackage {name: 'FluentValidation'})                      MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppServices'}), (pkg:NugetPackage {name: 'Mapster'})                               MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppServices'}), (pkg:NugetPackage {name: 'SlimMessageBus'})                        MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppServices'}), (pkg:NugetPackage {name: 'Scrutor'})                               MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppServices'}), (pkg:NugetPackage {name: 'DKNet.SlimBus.Extensions'})              MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Infra'}),       (pkg:NugetPackage {name: 'Microsoft.EntityFrameworkCore.SqlServer'}) MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Infra'}),       (pkg:NugetPackage {name: 'DKNet.EfCore.Events'})                   MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Infra'}),       (pkg:NugetPackage {name: 'DKNet.EfCore.Relational.Helpers'})       MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Infra'}),       (pkg:NugetPackage {name: 'SlimMessageBus.Host.Memory'})            MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Infra'}),       (pkg:NugetPackage {name: 'SlimMessageBus.Host.AzureServiceBus'})   MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Api'}),         (pkg:NugetPackage {name: 'Asp.Versioning.Http'})                   MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Api'}),         (pkg:NugetPackage {name: 'OpenTelemetry'})                         MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Api'}),         (pkg:NugetPackage {name: 'Microsoft.Extensions.Caching.Hybrid'})   MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.Api'}),         (pkg:NugetPackage {name: 'Microsoft.FeatureManagement.AspNetCore'}) MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppHost'}),     (pkg:NugetPackage {name: 'Aspire.Hosting.AppHost'})                MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppHost'}),     (pkg:NugetPackage {name: 'Aspire.Hosting.Redis'})                  MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.AppHost'}),     (pkg:NugetPackage {name: 'Aspire.Hosting.SqlServer'})              MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.App.Tests'}),   (pkg:NugetPackage {name: 'xunit'})                                 MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.App.Tests'}),   (pkg:NugetPackage {name: 'NetArchTest.Rules'})                     MERGE (p)-[:USES_PACKAGE]->(pkg);
MATCH (p:Project {name: 'Minimal.App.BDDTests'}),(pkg:NugetPackage {name: 'Reqnroll.NUnit'})                        MERGE (p)-[:USES_PACKAGE]->(pkg);

// ==================== FEATURES ====================
MERGE (:Feature {name: 'CustomerProfiles', description: 'Customer profile management', version: 'V1', dbSchema: 'pro'});

// ==================== CLASSES — DOMAINS ====================
MERGE (:Class {name: 'AggregateRoot',    project: 'Minimal.Domains', namespace: 'Minimal.Domains.Share',                          kind: 'class',  isAbstract: true,  isSealed: false, layer: 'Domains', pattern: 'DomainBase'});
MERGE (:Class {name: 'CustomerProfile',  project: 'Minimal.Domains', namespace: 'Minimal.Domains.Features.Profiles.Entities',     kind: 'class',  isAbstract: false, isSealed: false, layer: 'Domains', pattern: 'AggregateRoot', dbTable: 'CustomerProfiles', dbSchema: 'pro'});

// CustomerProfile properties
MATCH (c:Class {name: 'CustomerProfile'})
  MERGE (c)-[:HAS_PROPERTY]->(:Property {name: 'Email',        type: 'string',    isRequired: true,  maxLength: 150, isUnique: true});
MATCH (c:Class {name: 'CustomerProfile'})
  MERGE (c)-[:HAS_PROPERTY]->(:Property {name: 'Name',         type: 'string',    isRequired: true,  maxLength: 150});
MATCH (c:Class {name: 'CustomerProfile'})
  MERGE (c)-[:HAS_PROPERTY]->(:Property {name: 'MembershipNo', type: 'string',    isRequired: true,  maxLength: 50,  isUnique: true});
MATCH (c:Class {name: 'CustomerProfile'})
  MERGE (c)-[:HAS_PROPERTY]->(:Property {name: 'Phone',        type: 'string?',   isRequired: false, maxLength: 50});
MATCH (c:Class {name: 'CustomerProfile'})
  MERGE (c)-[:HAS_PROPERTY]->(:Property {name: 'Avatar',       type: 'string?',   isRequired: false, maxLength: 50});
MATCH (c:Class {name: 'CustomerProfile'})
  MERGE (c)-[:HAS_PROPERTY]->(:Property {name: 'BirthDay',     type: 'DateTime?', isRequired: false});

// CustomerProfile methods
MATCH (c:Class {name: 'CustomerProfile'})
  MERGE (c)-[:HAS_METHOD]->(:Method {name: 'Update', isAsync: false, visibility: 'public',   signature: 'Update(avatar, name, phoneNumber, birthday, userId)'});
MATCH (c:Class {name: 'CustomerProfile'})
  MERGE (c)-[:HAS_METHOD]->(:Method {name: '.ctor',  isAsync: false, visibility: 'public',   signature: 'CustomerProfile(name, membershipNo, email, phone, byUser)'});

// Inheritance
MATCH (child:Class {name: 'CustomerProfile'}), (parent:Class {name: 'AggregateRoot'})
  MERGE (child)-[:INHERITS]->(parent);

// ==================== CLASSES — APPSERVICES ====================
MERGE (:Class {name: 'CreateProfileRequest',           project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Actions', kind: 'record', isSealed: true,  layer: 'AppServices', pattern: 'Command',     httpMethod: 'POST'});
MERGE (:Class {name: 'CreateProfileCommandValidator',  project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Actions', kind: 'class',  isSealed: true,  layer: 'AppServices', pattern: 'Validator'});
MERGE (:Class {name: 'CreateProfileCommandHandler',    project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Actions', kind: 'class',  isSealed: true,  layer: 'AppServices', pattern: 'Handler'});
MERGE (:Class {name: 'UpdateProfileRequest',           project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Actions', kind: 'record', isSealed: false, layer: 'AppServices', pattern: 'Command',     httpMethod: 'PUT'});
MERGE (:Class {name: 'UpdateProfileCommandHandler',    project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Actions', kind: 'class',  isSealed: true,  layer: 'AppServices', pattern: 'Handler'});
MERGE (:Class {name: 'DeleteProfileRequest',           project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Actions', kind: 'record', isSealed: false, layer: 'AppServices', pattern: 'Command',     httpMethod: 'DELETE'});
MERGE (:Class {name: 'DeleteProfileCommandHandler',    project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Actions', kind: 'class',  isSealed: true,  layer: 'AppServices', pattern: 'Handler'});
MERGE (:Class {name: 'SpecGetCustomerProfile',         project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Specs',   kind: 'class',  isSealed: true,  layer: 'AppServices', pattern: 'Specification'});
MERGE (:Class {name: 'CustomerProfileDto',             project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1',          kind: 'record', isSealed: true,  layer: 'AppServices', pattern: 'DTO',         isGenerated: true});
MERGE (:Class {name: 'ProfileCreatedEvent',            project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Events',   kind: 'record', isSealed: true,  layer: 'AppServices', pattern: 'DomainEvent'});
MERGE (:Class {name: 'ProfileCreatedEventFromMemoryHandler', project: 'Minimal.AppServices', namespace: 'Minimal.AppServices.CustomerProfiles.V1.Events', kind: 'class', isSealed: true, layer: 'AppServices', pattern: 'EventHandler', scope: 'InProcess'});

// ==================== CLASSES — API ====================
MERGE (:Class {name: 'CustomerProfileV1Endpoint', project: 'Minimal.Api', namespace: 'Minimal.Api.ApiEndpoints', kind: 'class', isSealed: true, layer: 'Api', pattern: 'Endpoint', version: 1, route: '/customer-profiles'});

// ==================== CLASSES — INFRA ====================
MERGE (:Class {name: 'CoreDbContext',                              project: 'Minimal.Infra', namespace: 'Minimal.Infra.Contexts',                            kind: 'class', isSealed: false, layer: 'Infra', pattern: 'DbContext'});
MERGE (:Class {name: 'OwnedDataContext',                           project: 'Minimal.Infra', namespace: 'Minimal.Infra.Contexts',                            kind: 'class', isSealed: false, layer: 'Infra', pattern: 'DbContext'});
MERGE (:Class {name: 'CustomerProfileConfigs',                     project: 'Minimal.Infra', namespace: 'Minimal.Infra.Features.Profiles.Mappers',           kind: 'class', isSealed: true,  layer: 'Infra', pattern: 'EfConfig'});
MERGE (:Class {name: 'CustomerProfileStaticData',                  project: 'Minimal.Infra', namespace: 'Minimal.Infra.Features.Profiles.StaticData',        kind: 'class', isSealed: true,  layer: 'Infra', pattern: 'DataSeed'});
MERGE (:Class {name: 'CustomerProfileCreatedEmailNotificationHandler', project: 'Minimal.Infra', namespace: 'Minimal.Infra.Features.Profiles.ExternalEvents', kind: 'class', isSealed: true,  layer: 'Infra', pattern: 'EventHandler', scope: 'External'});
MERGE (:Class {name: 'InfraSetup',                                 project: 'Minimal.Infra', namespace: 'Minimal.Infra.Extensions',                          kind: 'class', isSealed: false, layer: 'Infra', pattern: 'Setup', isStatic: true});

// ==================== CQRS RELATIONSHIPS ====================
// Handler → HANDLES → Command
MATCH (h:Class {name: 'CreateProfileCommandHandler'}), (c:Class {name: 'CreateProfileRequest'})
  MERGE (h)-[:HANDLES]->(c);
MATCH (h:Class {name: 'UpdateProfileCommandHandler'}), (c:Class {name: 'UpdateProfileRequest'})
  MERGE (h)-[:HANDLES]->(c);
MATCH (h:Class {name: 'DeleteProfileCommandHandler'}), (c:Class {name: 'DeleteProfileRequest'})
  MERGE (h)-[:HANDLES]->(c);

// Validator → VALIDATES → Command
MATCH (v:Class {name: 'CreateProfileCommandValidator'}), (c:Class {name: 'CreateProfileRequest'})
  MERGE (v)-[:VALIDATES]->(c);

// Command → RETURNS → DTO
MATCH (c:Class {name: 'CreateProfileRequest'}), (d:Class {name: 'CustomerProfileDto'})
  MERGE (c)-[:RETURNS]->(d);
MATCH (c:Class {name: 'UpdateProfileRequest'}), (d:Class {name: 'CustomerProfileDto'})
  MERGE (c)-[:RETURNS]->(d);

// Handler → PUBLISHES → Event
MATCH (h:Class {name: 'CreateProfileCommandHandler'}), (e:Class {name: 'ProfileCreatedEvent'})
  MERGE (h)-[:PUBLISHES]->(e);

// EventHandlers → HANDLES → Event
MATCH (h:Class {name: 'ProfileCreatedEventFromMemoryHandler'}),          (e:Class {name: 'ProfileCreatedEvent'}) MERGE (h)-[:HANDLES]->(e);
MATCH (h:Class {name: 'CustomerProfileCreatedEmailNotificationHandler'}), (e:Class {name: 'ProfileCreatedEvent'}) MERGE (h)-[:HANDLES]->(e);

// Handlers → USES_SPEC → Specification
MATCH (h:Class {name: 'CreateProfileCommandHandler'}), (s:Class {name: 'SpecGetCustomerProfile'}) MERGE (h)-[:USES_SPEC]->(s);
MATCH (h:Class {name: 'UpdateProfileCommandHandler'}), (s:Class {name: 'SpecGetCustomerProfile'}) MERGE (h)-[:USES_SPEC]->(s);
MATCH (h:Class {name: 'DeleteProfileCommandHandler'}), (s:Class {name: 'SpecGetCustomerProfile'}) MERGE (h)-[:USES_SPEC]->(s);

// Spec → QUERIES → Entity
MATCH (s:Class {name: 'SpecGetCustomerProfile'}), (e:Class {name: 'CustomerProfile'})
  MERGE (s)-[:QUERIES]->(e);

// Endpoint → EXPOSES → Commands
MATCH (ep:Class {name: 'CustomerProfileV1Endpoint'}), (c:Class {name: 'CreateProfileRequest'}) MERGE (ep)-[:EXPOSES {verb: 'POST'}]->(c);
MATCH (ep:Class {name: 'CustomerProfileV1Endpoint'}), (c:Class {name: 'UpdateProfileRequest'}) MERGE (ep)-[:EXPOSES {verb: 'PUT'}]->(c);
MATCH (ep:Class {name: 'CustomerProfileV1Endpoint'}), (c:Class {name: 'DeleteProfileRequest'}) MERGE (ep)-[:EXPOSES {verb: 'DELETE'}]->(c);
// GET endpoints query the entity directly via MapGetList/MapGetById
MATCH (ep:Class {name: 'CustomerProfileV1Endpoint'}), (e:Class {name: 'CustomerProfile'})      MERGE (ep)-[:EXPOSES {verb: 'GET'}]->(e);

// Handlers → CREATES/UPDATES/DELETES → Entity
MATCH (h:Class {name: 'CreateProfileCommandHandler'}), (e:Class {name: 'CustomerProfile'}) MERGE (h)-[:CREATES]->(e);
MATCH (h:Class {name: 'UpdateProfileCommandHandler'}), (e:Class {name: 'CustomerProfile'}) MERGE (h)-[:UPDATES]->(e);
MATCH (h:Class {name: 'DeleteProfileCommandHandler'}), (e:Class {name: 'CustomerProfile'}) MERGE (h)-[:DELETES]->(e);

// EF Config and seeding
MATCH (c:Class {name: 'CustomerProfileConfigs'}),  (e:Class {name: 'CustomerProfile'}) MERGE (c)-[:CONFIGURES]->(e);
MATCH (d:Class {name: 'CustomerProfileStaticData'}),(e:Class {name: 'CustomerProfile'}) MERGE (d)-[:SEEDS]->(e);

// OwnedDataContext extends CoreDbContext
MATCH (child:Class {name: 'OwnedDataContext'}), (parent:Class {name: 'CoreDbContext'})
  MERGE (child)-[:INHERITS]->(parent);

// ==================== CLASS → PROJECT ====================
MATCH (c:Class), (p:Project) WHERE c.project = p.name
  MERGE (c)-[:IN_PROJECT]->(p);

// ==================== CLASS → FEATURE ====================
MATCH (c:Class), (f:Feature {name: 'CustomerProfiles'})
WHERE c.namespace CONTAINS 'CustomerProfile' OR c.namespace CONTAINS 'Profiles'
  MERGE (c)-[:PART_OF_FEATURE]->(f);

// ==================== USEFUL QUERIES (reference) ====================
// All node counts:           MATCH (n) RETURN labels(n)[0] AS label, count(n) AS count ORDER BY count DESC
// Project dependency graph:  MATCH p=(a:Project)-[:DEPENDS_ON*]->(b:Project) RETURN p
// Full CQRS flow:            MATCH p=(ep:Class)-[:EXPOSES]->(cmd:Class)<-[:HANDLES]-(h:Class)-[:CREATES|UPDATES|DELETES]->(e:Class) RETURN p
// Feature slice:             MATCH (c:Class)-[:PART_OF_FEATURE]->(f:Feature) RETURN c.name, c.pattern, c.layer ORDER BY c.layer
// Event fan-out:             MATCH p=(h:Class)-[:PUBLISHES]->(ev:Class)<-[:HANDLES]-(consumer:Class) RETURN p
// Layer violations:          MATCH (c:Class)-[:IN_PROJECT]->(p:Project)-[:IN_LAYER]->(l:Layer) WITH l.name AS layer, count(c) AS classCount RETURN layer, classCount ORDER BY classCount DESC
