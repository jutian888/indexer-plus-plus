// This file is the part of the Indexer++ project.
// Copyright (C) 2016 Anna Krykora <krykoraanna@gmail.com>. All rights reserved.
// Use of this source code is governed by a MIT-style license that can be found in the LICENSE file.

#include "QueryProcessor.h"

#include "OutputFormatter.h"
#include "SearchEngine.h"
#include "SearchQuery.h"

namespace indexer {

    using namespace std;

    vector<wstring> QueryProcessor::Process(const wstring& query_string, const wstring& format_string, int max_files) {
        std::unique_ptr<SearchEngine> engine(make_unique<SearchEngine>(nullptr, true));

        uSearchQuery query = DeserializeQuery(query_string);

        auto search_res = engine->Search(query.release());

        OutputFormatter fmt(search_res->Files.get(), format_string, max_files);
        return fmt.Format();
    }
} // namespace indexer