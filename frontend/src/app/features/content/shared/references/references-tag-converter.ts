/*
 * Squidex Headless CMS
 *
 * @license
 * Copyright (c) Squidex UG (haftungsbeschränkt). All rights reserved.
 */

import { AppLanguageDto, ContentDto, getContentValue, LocalizerService, TagConverter, TagValue } from '@app/shared/internal';

export class ReferencesTagsConverter implements TagConverter {
    public tags: ReadonlyArray<TagValue> = [];

    constructor(language: AppLanguageDto, contents: ReadonlyArray<ContentDto>,
        private readonly localizer: LocalizerService,
    ) {
        this.tags = this.createTags(language, contents);
    }

    public convertInput(input: string) {
        const result = this.tags.find(x => x.name === input);

        return result || null;
    }

    public convertValue(value: any) {
        const result = this.tags.find(x => x.id === value);

        return result || null;
    }

    private createTags(language: AppLanguageDto, contents: ReadonlyArray<ContentDto>): ReadonlyArray<TagValue> {
        if (contents.length === 0) {
            return [];
        }

        const values = contents.map(content => {
            const name =
                content.referenceFields
                    .map(f => getContentValue(content, language, f, false))
                    .map(v => v.formatted)
                    .defined()
                    .join(', ')
                || this.localizer.getOrKey('common.noValue');

            return new TagValue(content.id, name, content.id);
        });

        return values;
    }
}
