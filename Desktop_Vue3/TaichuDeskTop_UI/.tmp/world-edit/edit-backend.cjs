const fs=require('fs');
function edit(file,fn){const p='.tmp/world-edit/backend/'+file;fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n')));}
for(const f of ['CreateCardDto.cs','UpdateCardDto.cs'])edit(f,s=>s.replace('        public string? CoverImage { get; set; }','        public string? CoverImage { get; set; }\n\n        // 省略时保留既有关系；传空数组时清空出向关系。\n        public List<CreateRelationDto>? Relations { get; set; }'));
edit('CreateCardDto.cs',s=>s.replace('        public int Order { get; set; }','        public int Order { get; set; }\n        public string? ContextLabel { get; set; }'));
edit('WorldCardService.cs',s=>{
 s=s.replace('            await _context.WorldCards.AddAsync(card);','            await ApplyRelationsAsync(card, dto.Relations);\n            await _context.WorldCards.AddAsync(card);');
 s=s.replace('            // 返回创建的卡片','            WorldCache.InvalidateCards(_cache, card.OutRelations.Select(r => r.TargetCardId));\n\n            // 返回创建的卡片');
 s=s.replace('            // 3. 更新字段（只更新有值的）','            var affectedIds = card.OutRelations.Concat(card.InRelations)\n                .SelectMany(r => new[] { r.SourceCardId, r.TargetCardId }).Append(cardId).ToList();\n            await ApplyRelationsAsync(card, dto.Relations);\n\n            // 3. 更新字段（只更新有值的）');
 s=s.replace('            _context.WorldCards.Update(card);\n','');
 s=s.replace('WorldCache.InvalidateCards(_cache, card.OutRelations.Concat(card.InRelations)\n                .SelectMany(relation => new[] { relation.SourceCardId, relation.TargetCardId }).Append(cardId));','WorldCache.InvalidateCards(_cache, affectedIds.Concat(card.OutRelations.Select(r => r.TargetCardId)));');
 const helper=`        // 先验证完整关系集合，再与正文一起由一次 SaveChanges 提交，避免部分保存。
        private async Task ApplyRelationsAsync(WorldCard card, List<CreateRelationDto>? desired)
        {
            if (desired == null) return;
            foreach (var relation in desired) WorldValidation.Validate(relation);
            var ids = desired.Select(r => r.TargetCardId).ToArray();
            if (ids.Contains(card.Id) || ids.Distinct().Count() != ids.Length)
                throw new WorldOperationException(WorldErrorKind.Invalid, "不能关联自己或重复关联同一条目");
            var targets = await _context.WorldCards.Where(c => c.ProjectId == card.ProjectId && ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);
            if (targets.Count != ids.Length)
                throw new WorldOperationException(WorldErrorKind.Invalid, "关联目标不存在或不属于当前世界");

            foreach (var old in card.OutRelations.Where(r => !ids.Contains(r.TargetCardId)).ToList())
            {
                _context.WorldRelations.Remove(old);
                card.OutRelations.Remove(old);
            }
            foreach (var input in desired)
            {
                var relation = card.OutRelations.FirstOrDefault(r => r.TargetCardId == input.TargetCardId);
                if (relation != null) relation.RelationType = input.RelationType;
                else
                {
                    relation = new WorldRelation { Id = Guid.NewGuid(), SourceCard = card, SourceCardId = card.Id,
                        TargetCard = targets[input.TargetCardId], TargetCardId = input.TargetCardId,
                        RelationType = input.RelationType, CreatedAt = DateTime.UtcNow };
                    _context.WorldRelations.Add(relation);
                }
            }
        }

`;
 return s.replace('        private static CardResponseDto? EnsureProject',helper+'        private static CardResponseDto? EnsureProject');
});
